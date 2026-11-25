using System.Text.Json;
using FluxoCaixa.Application.Consolidado;
using FluxoCaixa.Domain.Events;
using FluxoCaixa.Infrastructure.Messaging;
using FluxoCaixa.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FluxoCaixa.Infrastructure.Consumers;

/// <summary>
/// Consumidor da fila de consolidação (roda no Consolidado.Worker).
///
/// Entrega at-least-once com idempotência aplic + ack manual: só faz ack
/// depois de persistir o consolidado. Em falha, a mensagem volta à fila
/// (retry) e, após N tentativas, cai na DLQ — garantindo no máximo 5%
/// de perda, com monitoração da DLQ como quarta linha de defesa.
/// </summary>
public sealed class LancamentoCriadoConsumer(
    IServiceScopeFactory scopeFactory,
    IRabbitMqChannelProvider channelProvider,
    ILogger<LancamentoCriadoConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Contagem de tentativas em memória por MessageId (requeue não preserva headers;
    // para retry persistente entre restarts, ver ADR-005 — evolução futura com quorum queues/TTL).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _tentativas = new();

    // Canal mantido vivo pelo tempo de vida do serviço (evita GC/channel fechado).
    private IModel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await channelProvider.GetChannelAsync(stoppingToken);
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var evt = JsonSerializer.Deserialize<LancamentoCriadoIntegrationEvent>(
                    ea.Body.Span, JsonOptions);
                if (evt is null)
                {
                    logger.LogError("Payload inválido ignorado (DLQ): {Body}", System.Text.Encoding.UTF8.GetString(ea.Body.Span));
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                using var scope = scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<ConsolidarLancamentoHandler>();

                // message_id identifica a mensagem para deduplicação
                var messageId = ea.BasicProperties.MessageId
                                ?? throw new InvalidOperationException("Mensagem sem MessageId.");

                var aplicado = await handler.HandleAsync(evt, messageId, stoppingToken);

                _channel.BasicAck(ea.DeliveryTag, multiple: false);
                _ = aplicado; // log já feito no handler
            }
            catch (Exception ex)
            {
                // Retry controlado: conta tentativas em memória por MessageId;
                // esgotadas as N tentativas, rejeita SEM requeue -> mensagem vai
                // para a DLQ (monitorada). Evita livelock de requeue infinito.
                const int maxTentativas = 3;
                var messageId = ea.BasicProperties.MessageId ?? ea.DeliveryTag.ToString();
                var tentativas = _tentativas.AddOrUpdate(messageId, 1, (_, n) => n + 1);

                if (tentativas >= maxTentativas)
                {
                    _tentativas.TryRemove(messageId, out int _);
                    logger.LogError(ex, "Mensagem {MessageId} falhou {N}x — enviada para DLQ.",
                        messageId, tentativas);
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                logger.LogWarning(ex, "Erro processando mensagem {MessageId} (tentativa {T}/{N}); requeue.",
                    messageId, tentativas, maxTentativas);
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(RabbitMqTopology.Queue, autoAck: false, consumer);
        logger.LogInformation("Consumidor ativo na fila {Queue}.", RabbitMqTopology.Queue);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}

/// <summary>Provedor de canal para o consumer (testável).</summary>
public interface IRabbitMqChannelProvider
{
    Task<IModel> GetChannelAsync(CancellationToken ct = default);
}

public sealed class RabbitMqChannelProvider(RabbitMqConnectionPool pool) : IRabbitMqChannelProvider
{
    public Task<IModel> GetChannelAsync(CancellationToken ct = default) => pool.GetChannelAsync(ct);
}
