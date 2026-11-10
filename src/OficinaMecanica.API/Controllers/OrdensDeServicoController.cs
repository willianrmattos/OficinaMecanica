using OficinaMecanica.Application.Commands.AprovarOrcamento;
using OficinaMecanica.Application.Commands.AtualizarStatusOrdem;
using OficinaMecanica.Application.Commands.CancelarOrdemDeServico;
using OficinaMecanica.Application.Commands.CriarOrdemDeServico;
using OficinaMecanica.Application.Commands.RecusarOrcamento;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Application.Queries.ListarOrdensDeServico;
using OficinaMecanica.Application.Queries.ObterExecucaoDaOrdem;
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

    /// <summary>Consulta detalhada da execução (produção) de uma ordem de serviço.</summary>
    /// <remarks>
    /// Somente leitura. O OS Service pergunta à Execução, repassando o token do chamador, e devolve o detalhe com <c>origem = "execucao"</c>.
    /// Se a Execução estiver indisponível (timeout, circuito aberto, erro 5xx) ou ainda não tiver a ordem enfileirada (404 dela),
    /// a resposta continua sendo 200, com <c>origem = "saga"</c>: só o status da ordem e o último estado conhecido da saga, sem
    /// nenhum dado da produção (<c>producao</c> nulo). <c>motivoDoFallback</c> diz qual dos dois casos ocorreu.
    /// </remarks>
    /// <param name="id">Id da ordem de serviço.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <response code="200">Detalhe da execução (origem <c>execucao</c>) ou último estado conhecido da saga (origem <c>saga</c>).</response>
    /// <response code="400">Id da ordem de serviço inválido.</response>
    /// <response code="401">Token ausente ou inválido.</response>
    /// <response code="404">A ordem de serviço não existe.</response>
    [HttpGet("{id:guid}/execucao")]
    [ProducesResponseType(typeof(ExecucaoDaOrdemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterExecucao(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _mediator.Send(new ObterExecucaoDaOrdemQuery(id), cancellationToken);
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

    // Só a entrega é manual: diagnóstico, orçamento, pagamento e execução avançam pelo fluxo automático.
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AtualizarStatus(Guid id, [FromBody] AtualizarStatusRequest request)
    {
        await _mediator.Send(new AtualizarStatusOrdemCommand(id, request.NovoStatus));
        return NoContent();
    }

    // 202: a decisão do cliente é publicada para o fluxo automático, que a processa de forma assíncrona.
    // O corpo traz o status atual da OS (ainda 'AguardandoAprovacao') e o número, para acompanhar pela consulta pública.
    [HttpPost("{id:guid}/aprovar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SolicitacaoAceitaDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AprovarOrcamento(Guid id)
    {
        var resultado = await _mediator.Send(new AprovarOrcamentoCommand(id));
        return Accepted(resultado);
    }

    [HttpPost("{id:guid}/recusar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SolicitacaoAceitaDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecusarOrcamento(Guid id, [FromBody] RecusarOrcamentoRequest? request)
    {
        var resultado = await _mediator.Send(new RecusarOrcamentoCommand(id, request?.Motivo));
        return Accepted(resultado);
    }

    // Cancelamento do atendimento antes da execução: o fluxo automático desfaz o que já existir (orçamento, pagamento, estoque).
    [HttpPost("{id:guid}/cancelar")]
    [ProducesResponseType(typeof(SolicitacaoAceitaDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar(Guid id, [FromBody] CancelarOrdemDeServicoRequest request)
    {
        var resultado = await _mediator.Send(new CancelarOrdemDeServicoCommand(id, request.Motivo));
        return Accepted(resultado);
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
public record CancelarOrdemDeServicoRequest(string Motivo);
