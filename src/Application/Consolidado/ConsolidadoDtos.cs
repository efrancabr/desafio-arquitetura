using FluxoCaixa.Domain.Entities;

namespace FluxoCaixa.Application.Consolidado;

public sealed record ConsolidadoQuery(Guid ComercianteId, DateOnly? Inicio, DateOnly? Fim);

public sealed record SaldoDiarioResponse(
    DateOnly Data,
    decimal TotalCreditos,
    decimal TotalDebitos,
    decimal Saldo,
    int QuantidadeLancamentos);

public sealed record ConsolidadoResponse(
    Guid ComercianteId,
    IReadOnlyList<SaldoDiarioResponse> Dias,
    decimal SaldoTotalPeriodo);

public static class ConsolidadoMapper
{
    public static ConsolidadoResponse Map(Guid comercianteId, IReadOnlyList<SaldoDiario> dias) => new(
        comercianteId,
        dias.Select(d => new SaldoDiarioResponse(d.Data, d.TotalCreditos, d.TotalDebitos, d.Saldo, d.QuantidadeLancamentos)).ToList(),
        dias.Sum(d => d.Saldo));
}
