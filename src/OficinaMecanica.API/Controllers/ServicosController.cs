using OficinaMecanica.Application.Commands.AtualizarServico;
using OficinaMecanica.Application.Commands.CriarServico;
using OficinaMecanica.Application.Commands.DesativarServico;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Application.Queries.ListarServicos;
using OficinaMecanica.Application.Queries.ObterServico;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OficinaMecanica.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ServicosController : ControllerBase
{
    private readonly IMediator _mediator;

    public ServicosController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ServicoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarServicoCommand command)
    {
        var resultado = await _mediator.Send(command);
        return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Id }, resultado);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ServicoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var resultado = await _mediator.Send(new ObterServicoPorIdQuery(id));
        return resultado != null ? Ok(resultado) : NotFound();
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginacaoResultDto<ServicoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 10, [FromQuery] bool apenasAtivos = true)
    {
        var resultado = await _mediator.Send(new ListarServicosQuery(pagina, tamanhoPagina, apenasAtivos));
        return Ok(resultado);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarServicoRequest request)
    {
        await _mediator.Send(new AtualizarServicoCommand(id, request.Nome, request.Descricao, request.Preco, request.TempoEstimadoMinutos));
        return NoContent();
    }

    [HttpPatch("{id:guid}/desativar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desativar(Guid id)
    {
        await _mediator.Send(new DesativarServicoCommand(id, Ativar: false));
        return NoContent();
    }

    [HttpPatch("{id:guid}/ativar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ativar(Guid id)
    {
        await _mediator.Send(new DesativarServicoCommand(id, Ativar: true));
        return NoContent();
    }
}

public record AtualizarServicoRequest(string Nome, string? Descricao, decimal Preco, int TempoEstimadoMinutos);
