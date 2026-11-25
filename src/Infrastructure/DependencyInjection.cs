using FluxoCaixa.Application.Abstractions;
using FluxoCaixa.Infrastructure.Consumers;
using FluxoCaixa.Infrastructure.Data;
using FluxoCaixa.Infrastructure.Messaging;
using FluxoCaixa.Infrastructure.Outbox;
using FluxoCaixa.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FluxoCaixa.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra infraestrutura COMPLETA (PostgreSQL + RabbitMQ + Outbox).
    /// Usado pelas aplicações de produção.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionString 'Postgres' ausente.");

        services.AddDbContext<FluxoCaixaDbContext>(o =>
            o.UseNpgsql(connString));

        services.AddScoped<ILancamentoRepository, LancamentoRepository>();
        services.AddScoped<IOutboxStore, EfOutboxStore>();
        services.AddSingleton<RabbitMqConnectionPool>();
        services.Configure<RabbitMqSettings>(configuration.GetSection(RabbitMqSettings.SectionName));
        services.AddHostedService<OutboxPublisher>();

        return services;
    }

    /// <summary>
    /// Registra APENAS a infraestrutura do lado Consolidado (worker + API de leitura).
    /// </summary>
    public static IServiceCollection AddConsolidadoInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionString 'Postgres' ausente.");

        services.AddDbContext<FluxoCaixaDbContext>(o =>
            o.UseNpgsql(connString));

        services.AddScoped<FluxoCaixa.Application.Consolidado.ISaldoDiarioStore, SaldoDiarioStore>();
        services.AddScoped<FluxoCaixa.Application.Consolidado.IProcessedMessageStore, ProcessedMessageStore>();
        services.AddSingleton<RabbitMqConnectionPool>();
        services.Configure<RabbitMqSettings>(configuration.GetSection(RabbitMqSettings.SectionName));
        services.AddSingleton<IRabbitMqChannelProvider, RabbitMqChannelProvider>();
        services.AddHostedService<LancamentoCriadoConsumer>();

        return services;
    }
}
