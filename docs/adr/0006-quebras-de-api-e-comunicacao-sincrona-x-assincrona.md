# ADR 0006: Quebras de API da Saga e quando usar comunicacao sincrona x assincrona

## Status
Aceito

## Contexto
Com a Saga (ADR 0003), decisoes que antes eram resolvidas no mesmo request HTTP passam a ser
processadas de forma assincrona e coordenada: aprovar o orcamento, por exemplo, deixa de ser
"mudar o status da OS e dar baixa no estoque na mesma transacao" para "registrar a decisao e deixar
a Saga reservar estoque e pedir o pagamento". Isso muda contratos HTTP publicos e exige uma regra
clara sobre quando uma chamada pode ser sincrona entre servicos.

## Decisao
### Quebras de API (documentadas e aceitas)
- `POST /api/ordens-de-servico/{id}/aprovar` e `.../recusar` continuam anonimos e na mesma rota (os
  links ja enviados por e-mail seguem validos), mas respondem **202 Accepted** em vez de 200 com o
  DTO: a decisao e publicada para a Saga, que a serializa contra o prazo de aprovacao. O handler
  valida o estado local e responde **409** se a OS nao esta em `AguardandoAprovacao`.
- `PATCH /api/ordens-de-servico/{id}/status` passa a aceitar so `Entregue`; `EmDiagnostico`,
  `AguardandoAprovacao`, `EmExecucao` e `Finalizada` agora vem de eventos da Execucao e do Billing
  (um unico escritor por transicao).
- Novos status `AguardandoPagamento` (7) e `Cancelada` (8); os valores existentes nao sao
  renumerados (o status e persistido como inteiro e viaja em `NovoStatus`).
- OS criadas antes da Saga nao participam dela: aprovar/recusar nelas responde 409.
- A consulta publica da OS por numero passa a expor o link de pagamento e o prazo quando houver.

### Sincrono x assincrono
- **Assincrono (RabbitMQ)**: todo o fluxo que muda estado em mais de um servico (a Saga). Nenhum
  servico le ou escreve o banco de outro.
- **Sincrono (REST)**: (a) clientes, staff e mecanicos chamando cada servico via APIM; (b) busca de
  JWKS para validar JWT; (c) sistemas externos (chamadas do Billing ao Mercado Pago e o webhook de
  notificacao que o Mercado Pago envia ao Billing); (d) consulta
  somente-leitura `GET /api/ordens-de-servico/{id}/execucao`, em que o OS Service consulta a
  Execucao com timeout, retry e circuit breaker, repassando o JWT do chamador, e cai no ultimo
  estado conhecido da Saga se a Execucao estiver indisponivel.
- Regra pratica: **escrita entre servicos e sempre assincrona; leitura entre servicos pode ser
  sincrona so se tiver degradacao definida**.

## Consequencias
### Positivas
- Cada transicao de status tem um unico escritor, sem corrida entre HTTP e Saga.
- A leitura sincrona nunca bloqueia o fluxo de negocio: se a Execucao cair, a Saga segue.

### Negativas
- Clientes que dependem do 200 com DTO em aprovar/recusar precisam tratar 202 e consultar o status.
- OS em andamento no momento da migracao precisam ser concluidas fora da Saga.

## Alternativas consideradas
- **Manter 200 e esperar a Saga responder**: acoplaria a latencia do HTTP ao Billing e a Execucao.
- **Mover aprovar/recusar para o Billing**: criaria um segundo endpoint anonimo e reabriria a corrida
  entre a decisao do cliente e o timeout, que a Saga hoje serializa.
- **Migrar OS em andamento criando instancias de Saga retroativas**: custo alto para dados que, no
  escopo do projeto, sao de demonstracao.
