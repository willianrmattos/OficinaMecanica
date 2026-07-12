namespace OficinaMecanica.Domain.Enums;

public enum StatusOrdemDeServico
{
    OrcamentoRecusado = 0,
    Recebida = 1,
    EmDiagnostico = 2,
    AguardandoAprovacao = 3,
    EmExecucao = 4,
    Finalizada = 5,
    Entregue = 6
}
