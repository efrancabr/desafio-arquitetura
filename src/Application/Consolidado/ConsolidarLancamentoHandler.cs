using System.Text.Json;
using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Enums;
using FluxoCaixa.Domain.Events;
using Microsoft.Extensions.Logging;

namespace FluxoCaixa.Application.Consolidado;

/// <summary>
/// Caso de uso do worker: consumir LancamentoCriado e atualizar o read model.
///
/// Idempotência: o mesmo evento entregue duas vezes (at-least-once) NÃO pode
/// somar o valor duas vezes. A tabela de lançamentos processados
/// (ProcessedMessages) garante o processamento exactly-once por mensagem.
/// </summary>
public sealed class ConsolidarLancamentoHandler
{
    private readonly ISaldoDiarioStore _store;
    private readonly IProcessedMessageStore _processed;
    private readonly ILogger<ConsolidarLancamentoHandler> _logger;

    public ConsolidarLancamentoHandler(
        ISaldoDiarioStore store,
        IProcessedMessageStore processed,
        ILogger<ConsolidarLancamentoHandler> logger)
    {
        _store = store;
        _processed = processed;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(LancamentoCriadoIntegrationEvent evt, string messageId, CancellationToken ct = default)
    {
        if (await _processed.JaProcessadoAsync(messageId, ct))
        {
            _logger.LogDebug("Mensagem {MessageId} já processada — ignorada (idempotência).", messageId);
            return false;
        }

        var data = DateOnly.Parse(evt.Data);
        var saldo = await _store.ObterAsync(evt.ComercianteId, data, ct)
                    ?? SaldoDiario.Novo(evt.ComercianteId, data);

        saldo.Aplicar(
            debito: evt.Tipo == (int)TipoLancamento.Debito ? evt.Valor : 0m,
            credito: evt.Tipo == (int)TipoLancamento.Credito ? evt.Valor : 0m);

        await _store.UpsertAsync(saldo, ct);
        await _processed.MarcarProcessadoAsync(messageId, evt.LancamentoId, ct);
        await _store.SalvarAsync(ct);

        _logger.LogInformation(
            "Consolidado atualizado: comerciante {ComercianteId}, data {Data}, saldo {Saldo}",
            evt.ComercianteId, evt.Data, saldo.Saldo);

        return true;
    }
}

/// <summary>Deduplicação de mensagens consumidas (exactly-once por messageId).</summary>
public interface IProcessedMessageStore
{
    Task<bool> JaProcessadoAsync(string messageId, CancellationToken ct = default);
    Task MarcarProcessadoAsync(string messageId, Guid lancamentoId, CancellationToken ct = default);
}
