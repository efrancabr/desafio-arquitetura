using FluxoCaixa.Application.Consolidado;
using Microsoft.Extensions.DependencyInjection;

namespace FluxoCaixa.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<FluxoCaixa.Application.Lancamentos.RegistrarLancamentoHandler>();
        services.AddScoped<FluxoCaixa.Application.Lancamentos.ListarLancamentosHandler>();
        return services;
    }

    /// <summary>
    /// Casos de uso do lado Consolidado (worker/API de leitura).
    /// Separado de AddApplication: a API de Lançamentos não conhece o consolidado.
    /// </summary>
    public static IServiceCollection AddConsolidadoApplication(this IServiceCollection services)
    {
        services.AddScoped<ConsolidarLancamentoHandler>();
        return services;
    }
}
