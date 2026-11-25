namespace FluxoCaixa.Domain.Enums;

/// <summary>
/// Natureza do lançamento no fluxo de caixa.
/// Débito reduz o saldo; crédito aumenta.
/// </summary>
public enum TipoLancamento
{
    Debito = 1,
    Credito = 2
}
