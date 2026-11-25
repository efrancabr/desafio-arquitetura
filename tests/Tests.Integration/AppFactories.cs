using FluxoCaixa.Infrastructure.Consumers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Integration;

/// <summary>
/// Sobe a API de Lançamentos in-memory apontando para Postgres/RabbitMQ de teste.
/// Usa variáveis de ambiente (lidas pelo HostBuilder default antes do Program.cs),
/// pois a configuration in-memory é adicionada depois da leitura da connection string.
/// Além da API, roda IN-PROCESS o consumidor de consolidação (o "worker") para
/// exercitar o fluxo completo: API -> Outbox -> RabbitMQ -> Consumer -> Read model.
/// </summary>
public sealed class LancamentosAppFactory : WebApplicationFactory<FluxoCaixa.Lancamentos.ApiMarker>
{
    private readonly string _pgConn;
    private readonly string _rabbitHost;
    private readonly int _rabbitPort;

    public LancamentosAppFactory(string pgConn, string rabbitHost, int rabbitPort)
    {
        _pgConn = pgConn;
        _rabbitHost = rabbitHost;
        _rabbitPort = rabbitPort;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _pgConn);
        Environment.SetEnvironmentVariable("RabbitMQ__Host", _rabbitHost);
        Environment.SetEnvironmentVariable("RabbitMQ__Port", _rabbitPort.ToString());

        // Consumer de consolidação rodando in-process (substitui o Consolidado.Worker)
        builder.ConfigureServices(services =>
        {
            services.AddConsolidadoSide();
        });
    }
}

file static class ServiceCollectionExtensions
{
    public static void AddConsolidadoSide(this IServiceCollection services)
    {
        // RabbitMQ settings já vêm das env vars (RabbitMQ__Host/Port/...) bindadas
        // automaticamente pelo IOptions na configuration default.
        services.AddSingleton<FluxoCaixa.Infrastructure.Messaging.RabbitMqConnectionPool>();
        services.AddSingleton<IRabbitMqChannelProvider, RabbitMqChannelProvider>();
        services.AddHostedService<LancamentoCriadoConsumer>();
        services.AddScoped<FluxoCaixa.Application.Consolidado.ConsolidarLancamentoHandler>();
        services.AddScoped<FluxoCaixa.Application.Consolidado.ISaldoDiarioStore, FluxoCaixa.Infrastructure.Repositories.SaldoDiarioStore>();
        services.AddScoped<FluxoCaixa.Application.Consolidado.IProcessedMessageStore, FluxoCaixa.Infrastructure.Repositories.ProcessedMessageStore>();
    }
}

/// <summary>Sobe a API de Consolidado (leitura) in-memory.</summary>
public sealed class ConsolidadoAppFactory : WebApplicationFactory<FluxoCaixa.Consolidado.ApiMarker>
{
    private readonly string _pgConn;

    public ConsolidadoAppFactory(string pgConn) => _pgConn = pgConn;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _pgConn);
    }
}
