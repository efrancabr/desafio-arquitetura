namespace FluxoCaixa.Domain.Common;

/// <summary>Resultado paginado genérico.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Pagina,
    int TamanhoPagina,
    long Total);
