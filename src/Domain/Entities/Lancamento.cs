using FluxoCaixa.Domain.Enums;

namespace FluxoCaixa.Domain.Entities;

/// <summary>
/// Lançamento individual do fluxo de caixa de um comerciante (débito ou crédito).
/// Regra de ouro: uma entidade nunca nasce em estado inválido.
/// </summary>
public sealed class Lancamento
{
    private Lancamento() { } // EF Core

    private Lancamento(
        Guid id,
        Guid comercianteId,
        DateOnly data,
        TipoLancamento tipo,
        decimal valor,
        string? descricao,
        string idempotencyKey,
        DateTime criadoEmUtc)
    {
        Id = id;
        ComercianteId = comercianteId;
        Data = data;
        Tipo = tipo;
        Valor = valor;
        Descricao = descricao;
        IdempotencyKey = idempotencyKey;
        CriadoEmUtc = criadoEmUtc;
    }

    public Guid Id { get; }
    public Guid ComercianteId { get; }
    public DateOnly Data { get; }
    public TipoLancamento Tipo { get; }
    public decimal Valor { get; }
    public string? Descricao { get; }
    public string IdempotencyKey { get; }
    public DateTime CriadoEmUtc { get; }

    /// <summary>
    /// Fábrica com validação de invariantes de domínio.
    /// Lança <see cref="DomainException"/> quando algum invariante é violado.
    /// </summary>
    public static Lancamento Criar(
        Guid comercianteId,
        DateOnly data,
        TipoLancamento tipo,
        decimal valor,
        string? descricao,
        string idempotencyKey,
        DateTime? criadoEmUtc = null)
    {
        if (comercianteId == Guid.Empty)
            throw new DomainException("ComercianteId é obrigatório.");

        if (idempotencyKey is null || idempotencyKey.Length is < 8 or > 128)
            throw new DomainException("IdempotencyKey deve ter entre 8 e 128 caracteres.");

        if (valor <= 0)
            throw new DomainException("Valor deve ser maior que zero.");

        if (valor > 1_000_000_000m)
            throw new DomainException("Valor excede o limite máximo suportado.");

        if (data > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            throw new DomainException("Data não pode estar no futuro.");

        if (descricao?.Length > 200)
            throw new DomainException("Descrição deve ter no máximo 200 caracteres.");

        return new Lancamento(
            Guid.NewGuid(),
            comercianteId,
            data,
            tipo,
            valor,
            descricao,
            idempotencyKey,
            criadoEmUtc ?? DateTime.UtcNow);
    }

    /// <summary>Contribuição do lançamento para o saldo do dia (crédito soma, débito subtrai).</summary>
    public decimal EfeitoNoSaldo => Tipo == TipoLancamento.Credito ? Valor : -Valor;
}

/// <summary>Violação de invariante de domínio.</summary>
public sealed class DomainException(string mensagem) : Exception(mensagem);
