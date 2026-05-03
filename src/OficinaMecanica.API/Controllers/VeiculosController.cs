using OficinaMecanica.Application.Commands.AtualizarVeiculo;
using OficinaMecanica.Application.Commands.CriarVeiculo;
using OficinaMecanica.Application.Commands.RemoverVeiculo;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Application.Queries.ListarVeiculos;
using OficinaMecanica.Application.Queries.ObterVeiculo;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OficinaMecanica.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VeiculosController : ControllerBase
{
    private readonly IMediator _mediator;

    public VeiculosController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(VeiculoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarVeiculoCommand command)
    {
        var resultado = await _mediator.Send(command);
        return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Id }, resultado);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VeiculoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var resultado = await _mediator.Send(new ObterVeiculoPorIdQuery(id));
        return resultado != null ? Ok(resultado) : NotFound();
    }

    [HttpGet("cliente/{clienteId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<VeiculoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarPorCliente(Guid clienteId)
    {
        var resultado = await _mediator.Send(new ListarVeiculosPorClienteQuery(clienteId));
        return Ok(resultado);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarVeiculoRequest request)
    {
        await _mediator.Send(new AtualizarVeiculoCommand(id, request.Marca, request.Modelo, request.Ano));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(Guid id)
    {
        await _mediator.Send(new RemoverVeiculoCommand(id));
        return NoContent();
    }
}

public record AtualizarVeiculoRequest(string Marca, string Modelo, int Ano);
