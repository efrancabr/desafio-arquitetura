using FluxoCaixa.Application.Consolidado;
using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Infrastructure.Repositories;

public sealed class SaldoDiarioStore(FluxoCaixaDbContext db) : ISaldoDiarioStore
{
    public Task<SaldoDiario?> ObterAsync(Guid comercianteId, DateOnly data, CancellationToken ct = default)
        => db.SaldosDiarios.FirstOrDefaultAsync(s => s.ComercianteId == comercianteId && s.Data == data, ct);

    public async Task UpsertAsync(SaldoDiario saldo, CancellationToken ct = default)
    {
        // O EF change-tracker já carrega a entidade: se o handler obteve a instância
        // via ObterAsync, ela já está rastreada e as alterações serão persistidas
        // pelo SaveChangesAsync. Só adicionamos quando NÃO está rastreada (nova).
        var jaRastreada = db.SaldosDiarios.Local.Any(s =>
            s.ComercianteId == saldo.ComercianteId && s.Data == saldo.Data);
        if (!jaRastreada)
            await db.SaldosDiarios.AddAsync(saldo, ct);
    }

    public async Task<IReadOnlyList<SaldoDiario>> ListarPeriodoAsync(Guid comercianteId, DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => await db.SaldosDiarios
            .Where(s => s.ComercianteId == comercianteId && s.Data >= inicio && s.Data <= fim)
            .OrderBy(s => s.Data)
            .ToListAsync(ct);

    public Task<int> SalvarAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

public sealed class ProcessedMessageStore(FluxoCaixaDbContext db) : IProcessedMessageStore
{
    public async Task<bool> JaProcessadoAsync(string messageId, CancellationToken ct = default)
        => await db.ProcessedMessages.AnyAsync(p => p.MessageId == messageId, ct);

    public async Task MarcarProcessadoAsync(string messageId, Guid lancamentoId, CancellationToken ct = default)
        => await db.ProcessedMessages.AddAsync(new ProcessedMessage { MessageId = messageId, LancamentoId = lancamentoId }, ct);
}
