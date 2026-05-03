using OficinaMecanica.Application.Commands.AtualizarCliente;
using OficinaMecanica.Application.Commands.CriarCliente;
using OficinaMecanica.Application.Commands.RemoverCliente;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Application.Queries.ListarClientes;
using OficinaMecanica.Application.Queries.ObterCliente;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OficinaMecanica.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ClientesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ClienteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarClienteCommand command)
    {
        var resultado = await _mediator.Send(command);
        return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Id }, resultado);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClienteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var resultado = await _mediator.Send(new ObterClientePorIdQuery(id));
        return resultado != null ? Ok(resultado) : NotFound();
    }

    [HttpGet("documento/{numero}")]
    [ProducesResponseType(typeof(ClienteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorDocumento(string numero)
    {
        var resultado = await _mediator.Send(new ObterClientePorDocumentoQuery(numero));
        return resultado != null ? Ok(resultado) : NotFound();
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginacaoResultDto<ClienteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 10, [FromQuery] string? filtroNome = null)
    {
        var resultado = await _mediator.Send(new ListarClientesQuery(pagina, tamanhoPagina, filtroNome));
        return Ok(resultado);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarClienteRequest request)
    {
        await _mediator.Send(new AtualizarClienteCommand(id, request.Nome, request.Email, request.Telefone));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(Guid id)
    {
        await _mediator.Send(new RemoverClienteCommand(id));
        return NoContent();
    }
}

public record AtualizarClienteRequest(string Nome, string? Email, string? Telefone);
