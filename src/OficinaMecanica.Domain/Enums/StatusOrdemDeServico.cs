namespace OficinaMecanica.Domain.Enums;

// O status e persistido como inteiro e viaja em mensagens (NovoStatus), entao os valores existentes
// nunca sao renumerados: os novos entram no fim, mesmo quebrando a ordem do fluxo.
public enum StatusOrdemDeServico
{
    OrcamentoRecusado = 0,
    Recebida = 1,
    EmDiagnostico = 2,
    AguardandoAprovacao = 3,
    EmExecucao = 4,
    Finalizada = 5,
    Entregue = 6,
    AguardandoPagamento = 7,
    Cancelada = 8
}
