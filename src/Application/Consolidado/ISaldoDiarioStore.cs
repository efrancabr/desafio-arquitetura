using FluxoCaixa.Domain.Entities;

namespace FluxoCaixa.Application.Consolidado;

/// <summary>Porta de leitura/escrita do read model de saldos diários.</summary>
public interface ISaldoDiarioStore
{
    Task<SaldoDiario?> ObterAsync(Guid comercianteId, DateOnly data, CancellationToken ct = default);
    Task UpsertAsync(SaldoDiario saldo, CancellationToken ct = default);
    Task<int> SalvarAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SaldoDiario>> ListarPeriodoAsync(Guid comercianteId, DateOnly inicio, DateOnly fim, CancellationToken ct = default);
}
