using MediatR;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Queries.ObterExecucaoDaOrdem;

public class ObterExecucaoDaOrdemQueryHandler : IRequestHandler<ObterExecucaoDaOrdemQuery, ExecucaoDaOrdemDto?>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly ISagaStateReader _saga;
    private readonly IExecucaoClient _execucao;
    private readonly ILogger<ObterExecucaoDaOrdemQueryHandler> _logger;

    public ObterExecucaoDaOrdemQueryHandler(
        IOrdemDeServicoRepository ordemRepository,
        ISagaStateReader saga,
        IExecucaoClient execucao,
        ILogger<ObterExecucaoDaOrdemQueryHandler> logger)
    {
        _ordemRepository = ordemRepository;
        _saga = saga;
        _execucao = execucao;
        _logger = logger;
    }

    // A OS e a saga vem SEMPRE do banco deste servico: so o detalhe da producao vem da Execucao, e so se ela responder. Se ela
    // nao responder (ou ainda nao tem a ordem), a resposta e 200 com o que este servico sabe, sem completar com o que nao tem.
    public async Task<ExecucaoDaOrdemDto?> Handle(ObterExecucaoDaOrdemQuery request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterPorIdAsync(request.OrdemDeServicoId, cancellationToken);
        if (ordem == null) return null;

        var saga = await _saga.ObterAsync(ordem.Id, cancellationToken);
        var resultado = await _execucao.ObterOrdemDeProducaoAsync(ordem.Id, cancellationToken);

        if (resultado is { Situacao: SituacaoDaConsultaAExecucao.Encontrada, Producao: { } producao })
        {
            return Montar(ordem.Id, ordem.Numero, ordem.Status.ToString(), saga, OrigemDaConsultaDeExecucao.Execucao, null, producao);
        }

        var motivo = resultado.Situacao == SituacaoDaConsultaAExecucao.NaoEncontrada
            ? MotivoDoFallbackDaExecucao.ProducaoNaoEnfileirada
            : MotivoDoFallbackDaExecucao.ExecucaoIndisponivel;

        // Debug para o 404 (caso normal de uma OS que a Execucao ainda nao recebeu); Warning para a indisponibilidade, que e o
        // que alguem precisa ver. So o id da OS entra no log.
        _logger.Log(
            motivo == MotivoDoFallbackDaExecucao.ExecucaoIndisponivel ? LogLevel.Warning : LogLevel.Debug,
            "Consulta de execução da ordem de serviço {OrdemDeServicoId}: usando o último estado conhecido da saga ({MotivoDoFallback}, {Detalhe}).",
            ordem.Id,
            motivo,
            resultado.Detalhe);

        return Montar(ordem.Id, ordem.Numero, ordem.Status.ToString(), saga, OrigemDaConsultaDeExecucao.Saga, motivo, null);
    }

    private static ExecucaoDaOrdemDto Montar(
        Guid ordemId, string numero, string statusDaOrdem, EstadoConhecidoDaSaga? saga, string origem, string? motivoDoFallback, ProducaoDaOrdemDto? producao) =>
        new(ordemId, numero, origem, statusDaOrdem, saga?.Estado, saga?.AtualizadaEm, motivoDoFallback, producao);
}
