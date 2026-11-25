using FluxoCaixa.Application.Abstractions;
using FluxoCaixa.Domain.Common;
using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Infrastructure.Repositories;

public sealed class LancamentoRepository(FluxoCaixaDbContext db) : ILancamentoRepository
{
    public Task<Lancamento?> ObterPorIdempotencyKeyAsync(Guid comercianteId, string idempotencyKey, CancellationToken ct = default)
        => db.Lancamentos.FirstOrDefaultAsync(
               l => l.ComercianteId == comercianteId && l.IdempotencyKey == idempotencyKey, ct);

    public async Task AdicionarAsync(Lancamento lancamento, CancellationToken ct = default)
        => await db.Lancamentos.AddAsync(lancamento, ct);

    public async Task<PagedResult<Lancamento>> ListarAsync(
        Guid comercianteId, DateOnly? inicio, DateOnly? fim, int pagina, int tamanhoPagina, CancellationToken ct = default)
    {
        var query = db.Lancamentos.Where(l => l.ComercianteId == comercianteId);
        if (inicio.HasValue) query = query.Where(l => l.Data >= inicio.Value);
        if (fim.HasValue) query = query.Where(l => l.Data <= fim.Value);

        var total = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.Data).ThenByDescending(l => l.CriadoEmUtc)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);

        return new PagedResult<Lancamento>(items, pagina, tamanhoPagina, total);
    }

    public Task<int> SalvarAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
