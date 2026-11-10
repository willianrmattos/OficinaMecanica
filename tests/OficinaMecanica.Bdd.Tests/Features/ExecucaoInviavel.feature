# language: pt
Funcionalidade: Execução inviável depois do pagamento
  Para que o cliente receba o dinheiro de volta quando o reparo não pode ser feito
  A saga estorna o pagamento e só devolve a peça ao estoque depois de o Billing confirmar o estorno

  Contexto:
    Dado uma oficina com o serviço "Troca de óleo" de 150 reais
    E a peça "Filtro de óleo" de 40 reais com 10 unidades em estoque
    E o cliente "Maria Souza" com um veículo de placa "ABC1D23"
    E o Billing gera o orçamento e o link de pagamento assim que recebe o pedido

  Cenário: A execução é inviável, o pagamento é estornado e o estoque volta depois
    Dado uma ordem de serviço com 3 unidades da peça "Filtro de óleo" paga e em execução
    Quando a Execução informa que a execução é inviável com o motivo "Peça descontinuada"
    Então a saga da ordem está em "Estornando"
    E o Billing recebeu o pedido de estorno do pagamento da ordem
    E a ordem de serviço está com o status "EmExecucao"
    E a reserva de estoque da ordem está ativa
    E o estoque da peça "Filtro de óleo" é de 7 unidades

    Quando o Billing confirma o estorno do pagamento
    Então a saga da ordem está em "Cancelada"
    E a ordem de serviço está com o status "Cancelada"
    E o motivo do cancelamento da ordem contém "Peça descontinuada"
    E a reserva de estoque da ordem foi liberada
    E o estoque da peça "Filtro de óleo" é de 10 unidades

  Cenário: O estorno falha e a saga espera uma ação manual sem devolver o estoque
    Dado o Billing não consegue estornar pagamentos
    E uma ordem de serviço com 3 unidades da peça "Filtro de óleo" paga e em execução
    Quando a Execução informa que a execução é inviável com o motivo "Peça descontinuada"
    Então a saga da ordem está em "EstornoPendenteManual"
    E a métrica "saga_estorno_pendente_manual" com a tag "causa" igual a "EstornoFalhou" foi incrementada 1 vez
    E a métrica "saga_cancelada" não foi incrementada
    E o Billing recebeu o pedido de estorno do pagamento da ordem
    E a ordem de serviço está com o status "EmExecucao"
    E a reserva de estoque da ordem está ativa
    E o estoque da peça "Filtro de óleo" é de 7 unidades

    Quando o Billing confirma o estorno do pagamento
    Então a saga da ordem está em "Cancelada"
    E a ordem de serviço está com o status "Cancelada"
    E a reserva de estoque da ordem foi liberada
    E o estoque da peça "Filtro de óleo" é de 10 unidades
