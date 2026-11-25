using System.Text.Json;
using FluxoCaixa.Application.Abstractions;
using FluxoCaixa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Infrastructure.Repositories;

/// <summary>Grava eventos de integração na tabela outbox_messages (mesma transação do negócio).</summary>
public sealed class EfOutboxStore(FluxoCaixaDbContext db) : IOutboxStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task AdicionarAsync<T>(T integrationEvent, CancellationToken ct = default) where T : class
    {
        await db.Outbox.AddAsync(new OutboxMessage
        {
            Tipo = integrationEvent.GetType().Name,
            Payload = JsonSerializer.Serialize(integrationEvent, Options),
            OcorridoEmUtc = DateTime.UtcNow
        }, ct);
    }
}
