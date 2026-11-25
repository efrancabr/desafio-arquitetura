using FluxoCaixa.Application.Abstractions;
using FluxoCaixa.Application.Lancamentos;
using FluxoCaixa.Domain.Common;
using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests.Unit;

public class RegistrarLancamentoHandlerTests
{
    private sealed class InMemoryRepo : ILancamentoRepository
    {
        public List<Lancamento> Dados { get; } = [];
        public Task<Lancamento?> ObterPorIdempotencyKeyAsync(Guid id, string key, CancellationToken ct = default)
            => Task.FromResult(Dados.FirstOrDefault(l => l.ComercianteId == id && l.IdempotencyKey == key));
        public Task AdicionarAsync(Lancamento l, CancellationToken ct = default) { Dados.Add(l); return Task.CompletedTask; }
        public Task<PagedResult<Lancamento>> ListarAsync(Guid id, DateOnly? i, DateOnly? f, int p, int t, CancellationToken ct = default)
            => Task.FromResult(new PagedResult<Lancamento>(Dados, p, t, Dados.Count));
        public Task<int> SalvarAsync(CancellationToken ct = default) => Task.FromResult(1);
    }

    private sealed class InMemoryOutbox : IOutboxStore
    {
        public List<object> Eventos { get; } = [];
        public Task AdicionarAsync<T>(T evt, CancellationToken ct = default) where T : class { Eventos.Add(evt!); return Task.CompletedTask; }
    }

    private static RegistrarLancamentoHandler CriarHandler(InMemoryRepo repo, InMemoryOutbox outbox)
        => new(repo, outbox, NullLogger<RegistrarLancamentoHandler>.Instance);

    private static RegistrarLancamentoCommand NovoComando(decimal valor = 100m, string? key = null)
        => new(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), TipoLancamento.Credito, valor, "desc", key ?? $"chave-{Guid.NewGuid():N}");

    [Fact]
    public async Task Registrar_grava_lancamento_e_evento_no_outbox()
    {
        var repo = new InMemoryRepo();
        var outbox = new InMemoryOutbox();
        var handler = CriarHandler(repo, outbox);

        var resp = await handler.HandleAsync(NovoComando());

        Assert.False(resp.JaExistia);
        Assert.Single(repo.Dados);
        Assert.Single(outbox.Eventos); // evento + entidade na mesma "transação lógica"
    }

    [Fact]
    public async Task Registrar_duas_vezes_com_mesma_chave_nao_duplica()
    {
        var repo = new InMemoryRepo();
        var outbox = new InMemoryOutbox();
        var handler = CriarHandler(repo, outbox);
        var cmd = NovoComando(key: "idem-key-123");

        var primeiro = await handler.HandleAsync(cmd);
        var segundo = await handler.HandleAsync(cmd);

        Assert.False(primeiro.JaExistia);
        Assert.True(segundo.JaExistia);
        Assert.Single(repo.Dados);       // não duplicou
        Assert.Single(outbox.Eventos);    // só um evento publicado
        Assert.Equal(primeiro.Id, segundo.Id);
    }

    [Fact]
    public async Task Registrar_com_valor_zero_lanca_DomainException()
    {
        var handler = CriarHandler(new InMemoryRepo(), new InMemoryOutbox());
        await Assert.ThrowsAsync<FluxoCaixa.Domain.Entities.DomainException>(
            () => handler.HandleAsync(NovoComando(valor: 0)));
    }
}
