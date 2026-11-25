namespace FluxoCaixa.Domain.Entities;

/// <summary>
/// Read model do consolidado: saldo acumulado por comerciante e dia.
/// É atualizado pelo worker de consolidação a partir dos eventos de lançamento.
/// </summary>
public sealed class SaldoDiario
{
    private SaldoDiario() { } // EF Core

    public Guid ComercianteId { get; private set; }
    public DateOnly Data { get; private set; }
    public decimal TotalDebitos { get; private set; }
    public decimal TotalCreditos { get; private set; }
    public DateTime AtualizadoEmUtc { get; private set; }

    public decimal Saldo => TotalCreditos - TotalDebitos;
    public int QuantidadeLancamentos { get; private set; }

    public static SaldoDiario Novo(Guid comercianteId, DateOnly data)
        => new()
        {
            ComercianteId = comercianteId,
            Data = data,
            AtualizadoEmUtc = DateTime.UtcNow
        };

    public void Aplicar(decimal debito, decimal credito)
    {
        TotalDebitos += debito;
        TotalCreditos += credito;
        QuantidadeLancamentos++;
        AtualizadoEmUtc = DateTime.UtcNow;
    }
}
