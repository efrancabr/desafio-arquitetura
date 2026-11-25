namespace FluxoCaixa.Domain.Events;

/// <summary>
/// Evento de integração publicado quando um lançamento é persistido.
/// É o contrato entre o domínio de Lançamentos e o domínio de Consolidado —
/// o único acoplamento permitido entre os dois contextos.
/// </summary>
public sealed record LancamentoCriadoIntegrationEvent
{
    public required Guid LancamentoId { get; init; }
    public required Guid ComercianteId { get; init; }
    public required string Data { get; init; }          // yyyy-MM-dd (DateOnly não é serializável nativamente)
    public required int Tipo { get; init; }            // 1=Debito, 2=Credito
    public required decimal Valor { get; init; }
    public string? Descricao { get; init; }
    public required DateTime OcorridoEmUtc { get; init; }

    public const string NomeTipo = "LancamentoCriado";
}
