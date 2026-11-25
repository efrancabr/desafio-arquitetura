using FluxoCaixa.Infrastructure.Outbox;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace FluxoCaixa.Infrastructure.Messaging;

/// <summary>
/// Pool único de conexão RabbitMQ com reconexão automática (AutomaticRecoveryEnabled)
/// e retry manual com backoff exponencial (sem dependência externa).
/// Compartilhado pelo publisher (API de Lançamentos) e consumer (Worker).
/// </summary>
public sealed class RabbitMqConnectionPool
{
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqConnectionPool> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnectionPool(Microsoft.Extensions.Options.IOptions<RabbitMqSettings> options, ILogger<RabbitMqConnectionPool> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<IModel> GetChannelAsync(CancellationToken ct = default)
    {
        var connection = await GetOrCreateConnectionAsync(ct);
        var channel = connection.CreateModel();
        DeclareTopology(channel);
        return channel;
    }

    private async Task<IConnection> GetOrCreateConnectionAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true }) return _connection;

        await _lock.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true }) return _connection;

            var tentativa = 0;
            while (true)
            {
                try
                {
                    var factory = new ConnectionFactory
                    {
                        HostName = _settings.Host,
                        Port = _settings.Port,
                        UserName = _settings.User,
                        Password = _settings.Password,
                        AutomaticRecoveryEnabled = true,
                        NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                        // ESSENCIAL: despacha entregas via path async (AsyncEventingBasicConsumer).
                        // Sem isso o dispatcher síncrono lança "Should never be called".
                        DispatchConsumersAsync = true
                    };
                    _connection = factory.CreateConnection($"fluxocaixa-{Environment.MachineName}");
                    _logger.LogInformation("Conectado ao RabbitMQ {Host}:{Port}.", _settings.Host, _settings.Port);
                    return _connection;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    tentativa++;
                    var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, tentativa), 30));
                    _logger.LogWarning(ex,
                        "RabbitMQ indisponível (tentativa {T}); reconectando em {Delay}s.",
                        tentativa, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static void DeclareTopology(IModel channel)
    {
        // Exchange principal + DLX
        channel.ExchangeDeclare(RabbitMqTopology.Exchange, ExchangeType.Fanout, durable: true, autoDelete: false);
        channel.ExchangeDeclare(RabbitMqTopology.Dlx, ExchangeType.Fanout, durable: true, autoDelete: false);

        // Fila principal com DLX configurado (mensagens rejeitadas/nack vão para a DLQ)
        channel.QueueDeclare(RabbitMqTopology.Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = RabbitMqTopology.Dlx
            });
        channel.QueueBind(RabbitMqTopology.Queue, RabbitMqTopology.Exchange, RabbitMqTopology.RoutingKeyLancamentoCriado);

        // Dead letter queue para inspeção manual / reprocessamento
        channel.QueueDeclare(RabbitMqTopology.Dlq, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(RabbitMqTopology.Dlq, RabbitMqTopology.Dlx, string.Empty);
    }
}

public sealed class RabbitMqSettings
{
    public const string SectionName = "RabbitMQ";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}
