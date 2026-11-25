using FluxoCaixa.Application.Consolidado;
using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Enums;
using FluxoCaixa.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests.Unit;

public class ConsolidarLancamentoHandlerTests
{
    private sealed class InMemorySaldoStore : ISaldoDiarioStore
    {
        private readonly Dictionary<(Guid, DateOnly), SaldoDiario> _dados = new();

        public Task<SaldoDiario?> ObterAsync(Guid id, DateOnly data, CancellationToken ct = default)
            => Task.FromResult(_dados.TryGetValue((id, data), out var s) ? s : null);

        public Task UpsertAsync(SaldoDiario saldo, CancellationToken ct = default)
        {
            _dados.TryAdd((saldo.ComercianteId, saldo.Data), saldo);
            return Task.CompletedTask;
        }

        public Task<int> SalvarAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<SaldoDiario>> ListarPeriodoAsync(Guid id, DateOnly i, DateOnly f, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SaldoDiario>>(_dados.Values.Where(s => s.Data >= i && s.Data <= f).ToList());
    }

    private sealed class InMemoryProcessedStore : IProcessedMessageStore
    {
        private readonly HashSet<string> _ids = [];
        public Task<bool> JaProcessadoAsync(string messageId, CancellationToken ct = default)
            => Task.FromResult(_ids.Contains(messageId));
        public Task MarcarProcessadoAsync(string messageId, Guid lancamentoId, CancellationToken ct = default)
        {
            _ids.Add(messageId);
            return Task.CompletedTask;
        }
    }

    private static ConsolidarLancamentoHandler CriarHandler(InMemorySaldoStore saldos, InMemoryProcessedStore processados)
        => new(saldos, processados, NullLogger<ConsolidarLancamentoHandler>.Instance);

    private static LancamentoCriadoIntegrationEvent NovoEvento(Guid comerciante, string data, int tipo, decimal valor)
        => new()
        {
            LancamentoId = Guid.NewGuid(),
            ComercianteId = comerciante,
            Data = data,
            Tipo = tipo,
            Valor = valor,
            OcorridoEmUtc = DateTime.UtcNow
        };

    [Fact]
    public async Task Primeiro_evento_cria_saldo_do_dia()
    {
        var saldos = new InMemorySaldoStore();
        var processados = new InMemoryProcessedStore();
        var handler = CriarHandler(saldos, processados);
        var comerciante = Guid.NewGuid();

        var aplicado = await handler.HandleAsync(
            NovoEvento(comerciante, "2025-06-01", (int)TipoLancamento.Credito, 150m), "msg-1");

        Assert.True(aplicado);
        var saldo = await saldos.ObterAsync(comerciante, new DateOnly(2025, 6, 1));
        Assert.NotNull(saldo);
        Assert.Equal(150m, saldo!.Saldo);
    }

    [Fact]
    public async Task Evento_duplicado_nao_soma_duas_vezes_idempotencia()
    {
        var saldos = new InMemorySaldoStore();
        var processados = new InMemoryProcessedStore();
        var handler = CriarHandler(saldos, processados);
        var comerciante = Guid.NewGuid();
        var evt = NovoEvento(comerciante, "2025-06-01", (int)TipoLancamento.Credito, 150m);

        await handler.HandleAsync(evt, "msg-1");
        var segunda = await handler.HandleAsync(evt, "msg-1"); // reentrega (at-least-once)

        Assert.False(segunda);
        var saldo = await saldos.ObterAsync(comerciante, new DateOnly(2025, 6, 1));
        Assert.Equal(150m, saldo!.Saldo); // não dobrou
    }

    [Fact]
    public async Task Debito_e_credito_no_mesmo_dia_liquidam_no_saldo()
    {
        var saldos = new InMemorySaldoStore();
        var processados = new InMemoryProcessedStore();
        var handler = CriarHandler(saldos, processados);
        var comerciante = Guid.NewGuid();

        await handler.HandleAsync(NovoEvento(comerciante, "2025-06-02", (int)TipoLancamento.Credito, 500m), "msg-1");
        await handler.HandleAsync(NovoEvento(comerciante, "2025-06-02", (int)TipoLancamento.Debito, 200m), "msg-2");

        var saldo = await saldos.ObterAsync(comerciante, new DateOnly(2025, 6, 2));
        Assert.Equal(300m, saldo!.Saldo);
        Assert.Equal(2, saldo.QuantidadeLancamentos);
    }
}
