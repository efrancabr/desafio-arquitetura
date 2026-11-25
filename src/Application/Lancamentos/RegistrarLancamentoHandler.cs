using FluxoCaixa.Application.Abstractions;
using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Events;
using FluxoCaixa.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FluxoCaixa.Application.Lancamentos;

/// <summary>
/// Caso de uso: registrar um lançamento.
///
/// Garante idempotência (IdempotencyKey) e consistência transacional via Outbox:
/// o lançamento e o evento são gravados na MESMA transação — se o broker estiver
/// fora do ar, o evento não é perdido, apenas atrasado (at-least-once).
/// </summary>
public sealed class RegistrarLancamentoHandler
{
    private readonly ILancamentoRepository _repository;
    private readonly IOutboxStore _outbox;
    private readonly ILogger<RegistrarLancamentoHandler> _logger;

    public RegistrarLancamentoHandler(
        ILancamentoRepository repository,
        IOutboxStore outbox,
        ILogger<RegistrarLancamentoHandler> logger)
    {
        _repository = repository;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<LancamentoResponse> HandleAsync(RegistrarLancamentoCommand cmd, CancellationToken ct = default)
    {
        // 1. Idempotência: mesma chave => mesmo resultado, sem duplicar (at-least-once seguro)
        var existente = await _repository.ObterPorIdempotencyKeyAsync(cmd.ComercianteId, cmd.IdempotencyKey, ct);
        if (existente is not null)
        {
            return Map(existente, jaExistia: true);
        }

        // 2. Invariantes de domínio
        var lancamento = Lancamento.Criar(
            cmd.ComercianteId, cmd.Data, cmd.Tipo, cmd.Valor, cmd.Descricao, cmd.IdempotencyKey);

        // 3. Mesma transação: entidade + evento de outbox
        await _repository.AdicionarAsync(lancamento, ct);
        await _outbox.AdicionarAsync(new LancamentoCriadoIntegrationEvent
        {
            LancamentoId = lancamento.Id,
            ComercianteId = lancamento.ComercianteId,
            Data = lancamento.Data.ToString("yyyy-MM-dd"),
            Tipo = (int)lancamento.Tipo,
            Valor = lancamentoValorNormalizado(lancamento),
            Descricao = lancamento.Descricao,
            OcorridoEmUtc = lancamento.CriadoEmUtc
        }, ct);

        await _repository.SalvarAsync(ct);

        _logger.LogInformation(
            "Lançamento {LancamentoId} registrado (comerciante {ComercianteId}, tipo {Tipo}, valor {Valor})",
            lancamento.Id, lancamento.ComercianteId, lancamento.Tipo, lancamento.Valor);

        return Map(lancamento, jaExistia: false);
    }

    private static decimal lancamentoValorNormalizado(Lancamento l) => l.Valor;

    private static LancamentoResponse Map(Lancamento l, bool jaExistia) => new(
        l.Id, l.ComercianteId, l.Data,
        l.Tipo == TipoLancamento.Credito ? "credito" : "debito",
        l.Valor, l.Descricao, l.CriadoEmUtc, jaExistia);
}
