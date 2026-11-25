using FluxoCaixa.Application.Consolidado;
using FluxoCaixa.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Consolidado.Api.Controllers;

[ApiController]
[Route("api/consolidado")]
[Produces("application/json")]
public sealed class ConsolidadoController(ISaldoDiarioStore store) : ControllerBase
{
    /// <summary>
    /// Relatório de saldo consolidado por dia para um comerciante.
    /// Sem período informado, retorna o dia corrente.
    /// </summary>
    [HttpGet("{comercianteId:guid}")]
    public async Task<IActionResult> Obter(
        Guid comercianteId,
        [FromQuery] DateOnly? inicio,
        [FromQuery] DateOnly? fim,
        CancellationToken ct)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var ini = inicio ?? hoje;
        var f = fim ?? hoje;

        if (ini > f)
            return BadRequest(new { erro = "inicio não pode ser maior que fim." });

        var dias = await store.ListarPeriodoAsync(comercianteId, ini, f, ct);
        return Ok(ConsolidadoMapper.Map(comercianteId, dias));
    }
}
