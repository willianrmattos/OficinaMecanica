# language: pt
Funcionalidade: Pagamento que não se conclui
  Para que a peça reservada volte ao estoque quando o cliente não paga no prazo
  A saga cancela o pagamento no Billing, que é a autoridade sobre o dinheiro, e só então cancela a ordem

  Contexto:
    Dado uma oficina com o serviço "Troca de óleo" de 150 reais
    E a peça "Filtro de óleo" de 40 reais com 10 unidades em estoque
    E o cliente "Maria Souza" com um veículo de placa "ABC1D23"
    E o Billing gera o orçamento e o link de pagamento assim que recebe o pedido

  Cenário: O prazo de pagamento vence e a reserva de estoque é liberada
    Dado uma ordem de serviço com 3 unidades da peça "Filtro de óleo" aprovada e aguardando o pagamento
    Quando passam 48 horas e o sweeper de prazos roda
    Então o sweeper publicou 1 prazo vencido
    E o prazo de Pagamento da ordem foi marcado como processado
    E a saga da ordem está em "CancelandoPagamento"
    E o Billing recebeu o comando "CancelarPagamento" 1 vez
    E a reserva de estoque da ordem está ativa
    E o estoque da peça "Filtro de óleo" é de 7 unidades

    Quando o Billing confirma o cancelamento do pagamento
    Então a saga da ordem está em "Cancelada"
    E a ordem de serviço está com o status "Cancelada"
    E o motivo do cancelamento da ordem contém "Pagamento não realizado"
    E a reserva de estoque da ordem foi liberada
    E o estoque da peça "Filtro de óleo" é de 10 unidades
    E a saga não enviou o comando "EstornarPagamento"
    E a saga não enviou o comando "CancelarProducao"
    E a métrica "saga_prazo_expirado" com a tag "tipo" igual a "Pagamento" foi incrementada 1 vez
    E a métrica "saga_cancelada" com a tag "estado_origem" igual a "CancelandoPagamento" foi incrementada 1 vez

  Cenário: O cliente paga no último instante e a ordem segue como paga
    Dado o Billing já recebeu o pagamento quando o pedido de cancelamento chega
    E uma ordem de serviço com 3 unidades da peça "Filtro de óleo" aprovada e aguardando o pagamento
    Quando passam 48 horas e o sweeper de prazos roda
    Então a saga da ordem está em "EmExecucao"
    E a ordem de serviço está com o status "EmExecucao"
    E o Billing recebeu o comando "CancelarPagamento" 1 vez
    E a Execução recebeu o comando "IniciarExecucao" 1 vez
    E o estoque da peça "Filtro de óleo" é de 7 unidades
    E a saga não enviou o comando "EstornarPagamento"
