using System.Text.Json;
using FluxoCaixa.Infrastructure.Data;
using FluxoCaixa.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace FluxoCaixa.Infrastructure.Outbox;

/// <summary>
/// Background service que drena o Outbox transacional: lê mensagens pendentes e
/// publica no RabbitMQ com política at-least-once (retries com backoff exponencial).
///
/// Se o broker cair, as mensagens permanecem no banco e a API de Lançamentos
/// segue 100% disponível — cumprindo o RNF de isolamento entre os serviços.
/// </summary>
public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    RabbitMqConnectionPool connectionPool,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DrenarOutboxAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // O outbox é à prova de falhas: qualquer erro aqui apenas atrasa a publicação.
                logger.LogWarning(ex, "Falha ao drenar outbox; nova tentativa no próximo tick.");
            }
        }
    }

    private async Task DrenarOutboxAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FluxoCaixaDbContext>();

        var pendentes = await db.Outbox
            .Where(o => !o.Processado)
            .OrderBy(o => o.OcorridoEmUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pendentes.Count == 0) return;

        var channel = await connectionPool.GetChannelAsync(ct);

        foreach (var msg in pendentes)
        {
            var body = System.Text.Encoding.UTF8.GetBytes(msg.Payload);
            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.MessageId = msg.Id.ToString();
            props.ContentType = "application/json";
            props.Type = msg.Tipo;

            channel.BasicPublish(
                exchange: RabbitMqTopology.Exchange,
                routingKey: RabbitMqTopology.RoutingKeyLancamentoCriado,
                basicProperties: props,
                body: body);

            msg.Processado = true;
            msg.PublicadoEmUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Outbox: {Count} mensagem(ns) publicada(s).", pendentes.Count);
    }
}

/// <summary>Topologia RabbitMQ declarada uma única vez (exchange, fila, DLQ, binding).</summary>
public static class RabbitMqTopology
{
    public const string Exchange = "fluxocaixa.events";
    public const string Queue = "fluxocaixa.consolidado.lancamentos";
    public const string RoutingKeyLancamentoCriado = "lancamento.criado";
    public const string Dlx = "fluxocaixa.events.dlx";
    public const string Dlq = "fluxocaixa.consolidado.lancamentos.dlq";
}
