# RFC 0002: Eventos de dominio in-process (MediatR) vs. broker externo

## Status
Aceito

## Resumo
Os Domain Events do monolito (ex.: `StatusOrdemAlteradoEvent`,
`OrdemDeServicoCriadaEvent`, `OrcamentoRecusadoEvent`) sao publicados e
consumidos in-process, via `INotificationHandler<T>` do proprio MediatR,
despachados pelo `AppDbContext` (que implementa `IUnitOfWork`) logo apos
`SaveChangesAsync` persistir a transacao. Nao ha message broker externo
(Service Bus, RabbitMQ, Kafka) envolvido.

## Motivacao
Varias operacoes do dominio precisam disparar side effects que nao sao a
responsabilidade central do agregado que os originou - por exemplo, toda
mudanca de status de uma Ordem de Servico deve notificar o cliente por
e-mail (se ele tiver `Email` cadastrado). Modelar isso direto dentro do
Handler do Command (ex.: `AprovarOrcamentoCommandHandler` chamando
`IEmailService` diretamente) acopla regra de negocio de escrita a uma
preocupacao transversal (notificacao), e obrigaria repetir a mesma chamada
em todo Command que muda status. Precisavamos de um jeito de desacoplar
"o que aconteceu no dominio" de "quem reage a isso", sem introduzir
infraestrutura de mensageria que o tamanho atual do sistema nao justifica.

## Proposta
Cada mudanca de estado relevante do dominio levanta um Domain Event (ex.:
`StatusOrdemAlteradoEvent`) guardado numa colecao na propria entidade.
`AppDbContext.SaveChangesAsync` (Unit of Work) coleta esses eventos de
todos os agregados rastreados, salva a transacao no banco e so entao
despacha os eventos via `IMediator.Publish` - garantindo que o evento so
e processado se a escrita realmente foi persistida. Um unico
`INotificationHandler<StatusOrdemAlteradoEvent>`
(`StatusOrdemAlteradoEventHandler`) cobre todas as transicoes de status
(aprovacao, recusa, avanco de diagnostico etc.), evitando um handler por
transicao especifica. Outros eventos (`OrdemDeServicoCriadaEvent`,
`OrcamentoRecusadoEvent`) alimentam `MetricasNegocioEventHandlers`, que
atualizam contadores/metricas de negocio expostos via OpenTelemetry.

## Alternativas consideradas
- **Chamar o side effect direto no Handler do Command** (sem Domain
  Event): mais simples de rastrear (sem indireção via `IMediator.Publish`),
  mas acopla cada Command que muda status a `IEmailService`/metricas
  diretamente - qualquer novo side effect exigiria editar todo Command
  existente que dispara aquela mudanca de estado.
- **Message broker externo (Azure Service Bus, RabbitMQ)**: desacopla de
  verdade produtor e consumidor (inclusive entre processos/servicos
  diferentes), permite retry/dead-letter e processamento assincrono real,
  mas adiciona infraestrutura (broker, serializacao, monitoramento de
  fila) para um cenario onde, hoje, so o proprio monolito consome os
  eventos que ele mesmo produz - nenhum outro servico do ecossistema
  (`OficinaMecanica.Seguranca`) precisa reagir a eventos de Ordem de
  Servico. Preterido em favor da opcao mais simples enquanto essa
  necessidade nao existir de fato.
- **Outbox pattern com processamento assincrono em background**: mais
  resiliente a falha entre "salvar no banco" e "processar o evento" (o
  MediatR in-process falha juntos, no mesmo request), mas overhead de
  implementacao nao justificado pelos side effects atuais (e-mail
  best-effort, metricas), que ja toleram falha isolada sem comprometer a
  escrita principal.

## Decisao
Domain Events publicados in-process via MediatR (`IMediator.Publish`),
despachados pelo `AppDbContext` apos `SaveChangesAsync` - sem broker
externo, revisitar essa decisao se/quando outro servico do ecossistema
precisar reagir a eventos de Ordem de Servico.
