# language: pt
Funcionalidade: Ordem de serviço do pedido ao reparo concluído
  Para que o cliente receba o veículo reparado depois de aprovar o orçamento e pagar
  A saga da ordem de serviço coordena a Execução e o Billing sem intervenção manual

  Contexto:
    Dado uma oficina com o serviço "Troca de óleo" de 150 reais
    E a peça "Filtro de óleo" de 40 reais com 10 unidades em estoque
    E o cliente "Maria Souza" com um veículo de placa "ABC1D23"
    E o Billing gera o orçamento e o link de pagamento assim que recebe o pedido

  Cenário: Caminho feliz até a ordem finalizada
    Quando o atendente abre uma ordem de serviço com 1 unidade do serviço "Troca de óleo" e 3 unidades da peça "Filtro de óleo"
    Então a saga da ordem está em "AguardandoDiagnostico"
    E a ordem de serviço está com o status "Recebida"
    E a Execução recebeu o comando "SolicitarDiagnostico" 1 vez
    E o estoque da peça "Filtro de óleo" é de 10 unidades

    Quando a Execução inicia o diagnóstico
    Então a saga da ordem está em "EmDiagnostico"
    E a ordem de serviço está com o status "EmDiagnostico"

    Quando a Execução conclui o diagnóstico com o laudo "Pastilhas de freio gastas"
    Então a saga da ordem está em "AguardandoAprovacao"
    E a ordem de serviço está com o status "AguardandoAprovacao"
    E o Billing recebeu o orçamento da ordem com o valor total de 270 reais
    E o estoque da peça "Filtro de óleo" é de 10 unidades

    Quando o cliente aprova o orçamento
    Então a saga da ordem está em "AguardandoPagamento"
    E a ordem de serviço está com o status "AguardandoPagamento"
    E a reserva de estoque da ordem está ativa
    E o estoque da peça "Filtro de óleo" é de 7 unidades
    E o Billing recebeu o comando "SolicitarPagamento" 1 vez
    E a ordem de serviço tem o link de pagamento
    E o cliente recebeu um e-mail com o assunto contendo "link de pagamento"

    Quando o Billing informa que o pagamento foi aprovado
    Então a saga da ordem está em "EmExecucao"
    E a ordem de serviço está com o status "EmExecucao"
    E a Execução recebeu o comando "IniciarExecucao" 1 vez

    Quando a Execução informa que o reparo foi finalizado
    Então a saga da ordem está em "Concluida"
    E a ordem de serviço está com o status "Finalizada"
    E o estoque da peça "Filtro de óleo" é de 7 unidades
    E a saga não enviou o comando "CancelarPagamento"
    E a saga não enviou o comando "CancelarOrcamento"
    E a saga não enviou o comando "CancelarProducao"
    E a saga não enviou o comando "EstornarPagamento"
    E a métrica "saga_iniciada" foi incrementada 1 vez
    E a métrica "saga_concluida" foi incrementada 1 vez
    E a métrica "saga_cancelada" não foi incrementada
    E a métrica "saga_orcamento_recusado" não foi incrementada
