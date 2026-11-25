using FluxoCaixa.Domain.Common;
using FluxoCaixa.Domain.Entities;

namespace FluxoCaixa.Application.Abstractions;

/// <summary>Porta de persistência (Hexagonal) — implementada por Infrastructure.</summary>
public interface ILancamentoRepository
{
    Task<Lancamento?> ObterPorIdempotencyKeyAsync(Guid comercianteId, string idempotencyKey, CancellationToken ct = default);
    Task AdicionarAsync(Lancamento lancamento, CancellationToken ct = default);
    Task<PagedResult<Lancamento>> ListarAsync(Guid comercianteId, DateOnly? inicio, DateOnly? fim, int pagina, int tamanhoPagina, CancellationToken ct = default);
    Task<int> SalvarAsync(CancellationToken ct = default);
}

/// <summary>Porta do Outbox (Transactional Outbox pattern).</summary>
public interface IOutboxStore
{
    Task AdicionarAsync<T>(T integrationEvent, CancellationToken ct = default) where T : class;
}

/// <summary>
/// Porta de publicação de eventos. A implementação de produção lê o Outbox e publica no RabbitMQ.
/// Em testes, uma implementação in-memory captura os eventos para asserção.
/// </summary>
public interface IEventPublisher
{
    Task PublicarAsync<T>(T integrationEvent, CancellationToken ct = default) where T : class;
}
