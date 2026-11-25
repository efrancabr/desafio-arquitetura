using FluxoCaixa.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FluxoCaixa.Application.Lancamentos;

/// <summary>Caso de uso: listar lançamentos de um comerciante com filtros e paginação.</summary>
public sealed class ListarLancamentosHandler
{
    private readonly ILancamentoRepository _repository;
    private readonly ILogger<ListarLancamentosHandler> _logger;

    public ListarLancamentosHandler(ILancamentoRepository repository, ILogger<ListarLancamentosHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<Domain.Common.PagedResult<Domain.Entities.Lancamento>> HandleAsync(
        ListarLancamentosQuery query, CancellationToken ct = default)
    {
        if (query.Pagina < 1) throw new ArgumentException("Página deve ser >= 1.");
        if (query.TamanhoPagina is < 1 or > 200)
            throw new ArgumentException("Tamanho da página deve estar entre 1 e 200.");
        if (query.Inicio.HasValue && query.Fim.HasValue && query.Inicio > query.Fim)
            throw new ArgumentException("Início não pode ser maior que o fim.");

        return _repository.ListarAsync(
            query.ComercianteId, query.Inicio, query.Fim, query.Pagina, query.TamanhoPagina, ct);
    }
}
