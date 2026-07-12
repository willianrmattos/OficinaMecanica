using OficinaMecanica.Application.Commands.AprovarOrcamento;
using OficinaMecanica.Application.Commands.AtualizarStatusOrdem;
using OficinaMecanica.Application.Commands.CriarOrdemDeServico;
using OficinaMecanica.Application.Commands.RecusarOrcamento;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Application.Queries.ListarOrdensDeServico;
using OficinaMecanica.Application.Queries.ObterOrdemDeServico;
using OficinaMecanica.Application.Queries.ObterTempoMedioServicos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OficinaMecanica.API.Controllers;

[ApiController]
[Route("api/ordens-de-servico")]
[Authorize]
public class OrdensDeServicoController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdensDeServicoController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrdemDeServicoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarOrdemDeServicoCommand command)
    {
        var resultado = await _mediator.Send(command);
        return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Id }, resultado);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrdemDeServicoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var resultado = await _mediator.Send(new ObterOrdemDeServicoPorIdQuery(id));
        return resultado != null ? Ok(resultado) : NotFound();
    }

    [HttpGet("numero/{numero}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OrdemDeServicoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorNumero(string numero)
    {
        var resultado = await _mediator.Send(new ObterOrdemDeServicoPorNumeroQuery(numero));
        return resultado != null ? Ok(resultado) : NotFound();
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginacaoResultDto<OrdemDeServicoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 10, [FromQuery] string? filtroStatus = null)
    {
        var resultado = await _mediator.Send(new ListarOrdensDeServicoQuery(pagina, tamanhoPagina, filtroStatus));
        return Ok(resultado);
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AtualizarStatus(Guid id, [FromBody] AtualizarStatusRequest request)
    {
        await _mediator.Send(new AtualizarStatusOrdemCommand(id, request.NovoStatus));
        return NoContent();
    }

    [HttpPost("{id:guid}/aprovar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OrdemDeServicoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AprovarOrcamento(Guid id)
    {
        var resultado = await _mediator.Send(new AprovarOrcamentoCommand(id));
        return Ok(resultado);
    }

    [HttpPost("{id:guid}/recusar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OrdemDeServicoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecusarOrcamento(Guid id, [FromBody] RecusarOrcamentoRequest? request)
    {
        var resultado = await _mediator.Send(new RecusarOrcamentoCommand(id, request?.Motivo));
        return Ok(resultado);
    }

    [HttpGet("tempo-medio")]
    [ProducesResponseType(typeof(TempoMedioServicoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterTempoMedio()
    {
        var resultado = await _mediator.Send(new ObterTempoMedioServicosQuery());
        return Ok(resultado);
    }
}

public record AtualizarStatusRequest(int NovoStatus);
public record RecusarOrcamentoRequest(string? Motivo);
