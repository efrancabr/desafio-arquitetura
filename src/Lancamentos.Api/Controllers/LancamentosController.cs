using FluxoCaixa.Application.Lancamentos;
using Microsoft.AspNetCore.Mvc;

namespace FluxoCaixa.Lancamentos.Api.Controllers;

[ApiController]
[Route("api/lancamentos")]
[Produces("application/json")]
public sealed class LancamentosController(
    RegistrarLancamentoHandler registrar,
    ListarLancamentosHandler listar) : ControllerBase
{
    /// <summary>Registra um lançamento (débito ou crédito). Idempotente via IdempotencyKey.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(LancamentoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(LancamentoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Registrar(
        [FromBody] RegistrarLancamentoRequest request,
        CancellationToken ct)
    {
        var tipo = request.Tipo?.ToLowerInvariant() switch
        {
            "debito" or "débito" => Domain.Enums.TipoLancamento.Debito,
            "credito" or "crédito" => Domain.Enums.TipoLancamento.Credito,
            _ => (Domain.Enums.TipoLancamento)0 // inválido; o validador rejeita
        };

        var cmd = new RegistrarLancamentoCommand(
            request.ComercianteId,
            request.Data,
            tipo,
            request.Valor,
            request.Descricao,
            request.IdempotencyKey);

        var resultado = await registrar.HandleAsync(cmd, ct);

        // 200 quando já existia (idempotência), 201 quando criado
        return resultado.JaExistia
            ? Ok(resultado)
            : StatusCode(StatusCodes.Status201Created, resultado);
    }

    /// <summary>Lista lançamentos do comerciante com filtro de período e paginação.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] Guid comercianteId,
        [FromQuery] DateOnly? inicio,
        [FromQuery] DateOnly? fim,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 50,
        CancellationToken ct = default)
    {
        var resultado = await listar.HandleAsync(
            new ListarLancamentosQuery(comercianteId, inicio, fim, pagina, tamanhoPagina), ct);

        return Ok(new
        {
            total = resultado.Total,
            pagina = resultado.Pagina,
            tamanhoPagina = resultado.TamanhoPagina,
            items = resultado.Items.Select(i => new LancamentoResponse(
                i.Id, i.ComercianteId, i.Data,
                i.Tipo == Domain.Enums.TipoLancamento.Credito ? "credito" : "debito",
                i.Valor, i.Descricao, i.CriadoEmUtc, JaExistia: false))
        });
    }
}

public sealed record RegistrarLancamentoRequest(
    Guid ComercianteId,
    DateOnly Data,
    string Tipo,
    decimal Valor,
    string? Descricao,
    string IdempotencyKey);
