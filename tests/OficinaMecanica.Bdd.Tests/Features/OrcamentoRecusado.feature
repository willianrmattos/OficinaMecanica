# language: pt
Funcionalidade: Orçamento que não segue para pagamento
  Para que a oficina não segure peça nem cobre de quem não aceitou o orçamento
  A saga encerra a ordem quando o cliente recusa ou deixa o prazo de aprovação vencer

  Contexto:
    Dado uma oficina com o serviço "Troca de óleo" de 150 reais
    E a peça "Filtro de óleo" de 40 reais com 10 unidades em estoque
    E o cliente "Maria Souza" com um veículo de placa "ABC1D23"
    E o Billing gera o orçamento e o link de pagamento assim que recebe o pedido

  Cenário: O cliente recusa o orçamento
    Dado uma ordem de serviço com 3 unidades da peça "Filtro de óleo" e o orçamento aguardando a decisão do cliente
    Quando o cliente recusa o orçamento com o motivo "Achei caro"
    Então a saga da ordem está em "OrcamentoRecusado"
    E a ordem de serviço está com o status "OrcamentoRecusado"
    E o Billing recebeu o pedido de cancelamento do orçamento com o motivo contendo "Achei caro"
    E o estoque da peça "Filtro de óleo" é de 10 unidades
    E a saga não enviou o comando "SolicitarPagamento"
    E a saga não enviou o comando "CancelarPagamento"
    E a saga não enviou o comando "EstornarPagamento"
    E a saga não enviou o comando "CancelarProducao"
    E a métrica "saga_orcamento_recusado" com a tag "causa" igual a "ClienteRecusouOrcamento" foi incrementada 1 vez
    E a métrica "saga_prazo_expirado" não foi incrementada

  Cenário: O prazo de aprovação vence sem decisão do cliente
    Dado uma ordem de serviço com 3 unidades da peça "Filtro de óleo" e o orçamento aguardando a decisão do cliente
    Quando passam 72 horas e o sweeper de prazos roda
    Então o sweeper publicou 1 prazo vencido
    E a saga da ordem está em "OrcamentoRecusado"
    E a ordem de serviço está com o status "OrcamentoRecusado"
    E o prazo de Aprovacao da ordem foi marcado como processado
    E a métrica "saga_prazo_expirado" com a tag "tipo" igual a "Aprovacao" foi incrementada 1 vez
    E a métrica "saga_orcamento_recusado" com a tag "causa" igual a "PrazoExpirado" foi incrementada 1 vez
    E o Billing recebeu o pedido de cancelamento do orçamento com o motivo contendo "Prazo para aprovação"
    E o estoque da peça "Filtro de óleo" é de 10 unidades
    E a saga não enviou o comando "SolicitarPagamento"

  Cenário: A aprovação que chega depois de o prazo decidir não reabre a ordem
    Dado uma ordem de serviço com 3 unidades da peça "Filtro de óleo" e o orçamento aguardando a decisão do cliente
    Quando passam 72 horas e o sweeper de prazos roda
    Então a saga da ordem está em "OrcamentoRecusado"
    Quando o cliente tenta aprovar o orçamento
    Então a API respondeu 409
    E a saga da ordem está em "OrcamentoRecusado"
    E o estoque da peça "Filtro de óleo" é de 10 unidades
    E a saga não enviou o comando "SolicitarPagamento"
