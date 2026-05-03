using OficinaMecanica.Application.Commands.AdicionarEstoquePeca;
using OficinaMecanica.Application.Commands.AtualizarPeca;
using OficinaMecanica.Application.Commands.CriarPeca;
using OficinaMecanica.Application.Commands.DesativarPeca;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Application.Queries.ListarPecas;
using OficinaMecanica.Application.Queries.ObterPeca;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OficinaMecanica.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PecasController : ControllerBase
{
    private readonly IMediator _mediator;

    public PecasController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PecaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarPecaCommand command)
    {
        var resultado = await _mediator.Send(command);
        return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Id }, resultado);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PecaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var resultado = await _mediator.Send(new ObterPecaPorIdQuery(id));
        return resultado != null ? Ok(resultado) : NotFound();
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginacaoResultDto<PecaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 10, [FromQuery] bool apenasAtivos = true)
    {
        var resultado = await _mediator.Send(new ListarPecasQuery(pagina, tamanhoPagina, apenasAtivos));
        return Ok(resultado);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarPecaRequest request)
    {
        await _mediator.Send(new AtualizarPecaCommand(id, request.Nome, request.Descricao, request.CodigoReferencia, request.PrecoUnitario, request.EstoqueMinimo));
        return NoContent();
    }

    [HttpPatch("{id:guid}/estoque")]
    [ProducesResponseType(typeof(PecaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdicionarEstoque(Guid id, [FromBody] AdicionarEstoqueRequest request)
    {
        var resultado = await _mediator.Send(new AdicionarEstoquePecaCommand(id, request.Quantidade));
        return Ok(resultado);
    }

    [HttpPatch("{id:guid}/desativar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desativar(Guid id)
    {
        await _mediator.Send(new DesativarPecaCommand(id, Ativar: false));
        return NoContent();
    }

    [HttpPatch("{id:guid}/ativar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ativar(Guid id)
    {
        await _mediator.Send(new DesativarPecaCommand(id, Ativar: true));
        return NoContent();
    }
}

public record AtualizarPecaRequest(string Nome, string? Descricao, string? CodigoReferencia, decimal PrecoUnitario, int EstoqueMinimo);
public record AdicionarEstoqueRequest(int Quantidade);
