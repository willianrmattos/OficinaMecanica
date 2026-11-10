# language: pt
Funcionalidade: Cancelamento do atendimento e falhas de negócio
  Para que cada cancelamento desfaça exatamente o que já existe
  A saga compensa só as etapas já executadas e não deixa a ordem parada

  Contexto:
    Dado uma oficina com o serviço "Troca de óleo" de 150 reais
    E a peça "Filtro de óleo" de 40 reais com 10 unidades em estoque
    E o cliente "Maria Souza" com um veículo de placa "ABC1D23"
    E o Billing gera o orçamento e o link de pagamento assim que recebe o pedido

  Cenário: O atendente cancela a ordem enquanto o orçamento aguarda o cliente
    Dado uma ordem de serviço com 3 unidades da peça "Filtro de óleo" e o orçamento aguardando a decisão do cliente
    Quando o atendente cancela a ordem de serviço com o motivo "Cliente desistiu por telefone"
    Então a saga da ordem está em "Cancelada"
    E a ordem de serviço está com o status "Cancelada"
    E o motivo do cancelamento da ordem contém "Cliente desistiu por telefone"
    E a Execução recebeu o comando "CancelarProducao" 1 vez
    E o Billing recebeu o pedido de cancelamento do orçamento com o motivo contendo "Cliente desistiu por telefone"
    E o estoque da peça "Filtro de óleo" é de 10 unidades
    E a saga não enviou o comando "SolicitarPagamento"

  Cenário: O atendente cancela a ordem enquanto o cliente ainda não pagou
    Dado uma ordem de serviço com 3 unidades da peça "Filtro de óleo" aprovada e aguardando o pagamento
    Quando o atendente cancela a ordem de serviço com o motivo "Cliente desistiu por telefone"
    Então a saga da ordem está em "CancelandoPagamento"
    E o Billing recebeu o comando "CancelarPagamento" 1 vez
    E a Execução recebeu o comando "CancelarProducao" 1 vez
    E o estoque da peça "Filtro de óleo" é de 7 unidades

    Quando o Billing confirma o cancelamento do pagamento
    Então a saga da ordem está em "Cancelada"
    E a ordem de serviço está com o status "Cancelada"
    E o estoque da peça "Filtro de óleo" é de 10 unidades

  Cenário: O cliente aprova mas a peça acabou no estoque
    Dado uma ordem de serviço com 3 unidades da peça "Filtro de óleo" e o orçamento aguardando a decisão do cliente
    E a peça "Filtro de óleo" ficou com apenas 1 unidade em estoque depois da abertura da ordem
    Quando o cliente aprova o orçamento
    Então a saga da ordem está em "Cancelada"
    E a ordem de serviço está com o status "Cancelada"
    E o motivo do cancelamento da ordem contém "Estoque insuficiente"
    E o Billing recebeu o pedido de cancelamento do orçamento com o motivo contendo "Estoque insuficiente"
    E o estoque da peça "Filtro de óleo" é de 1 unidade
    E a saga não enviou o comando "SolicitarPagamento"

  Cenário: O cancelamento do pagamento falha tecnicamente e a saga fica à espera do reprocessamento
    Dado o Billing falha tecnicamente ao cancelar pagamentos
    E uma ordem de serviço com 3 unidades da peça "Filtro de óleo" aprovada e aguardando o pagamento
    Quando passam 48 horas e o sweeper de prazos roda
    Então a saga da ordem está em "CancelandoPagamento"
    E a saga recebeu a falha técnica do comando "CancelarPagamento"
    E a saga da ordem continua em "CancelandoPagamento"
    E a métrica "saga_compensacao_falhou" com a tag "comando" igual a "CancelarPagamento" foi incrementada 1 vez
    E o Billing recebeu o comando "CancelarPagamento" 1 vez
    E a ordem de serviço está com o status "AguardandoPagamento"
    E a reserva de estoque da ordem está ativa
    E o estoque da peça "Filtro de óleo" é de 7 unidades
