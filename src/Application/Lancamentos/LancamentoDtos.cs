using FluxoCaixa.Domain.Enums;

namespace FluxoCaixa.Application.Lancamentos;

public sealed record RegistrarLancamentoCommand(
    Guid ComercianteId,
    DateOnly Data,
    TipoLancamento Tipo,
    decimal Valor,
    string? Descricao,
    string IdempotencyKey);

public sealed record LancamentoResponse(
    Guid Id,
    Guid ComercianteId,
    DateOnly Data,
    string Tipo,
    decimal Valor,
    string? Descricao,
    DateTime CriadoEmUtc,
    bool JaExistia);

public sealed record ListarLancamentosQuery(
    Guid ComercianteId,
    DateOnly? Inicio,
    DateOnly? Fim,
    int Pagina = 1,
    int TamanhoPagina = 50);
