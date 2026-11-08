# RFC 0005: Decomposicao em microsservicos com Saga orquestrada

## Status
Aceito

## Resumo
O monolito `OficinaMecanica` passa a ser o **OS Service** (cadastros, ordem de
servico, consulta/historico, notificacao por e-mail e **orquestrador da Saga**) e
duas responsabilidades saem para repositorios proprios: `OficinaMecanica.Billing`
(orcamento e pagamento via Mercado Pago, banco SQL proprio) e
`OficinaMecanica.Execucao` (fila de execucao, diagnostico e reparo, MongoDB Atlas).
Os tres servicos (mais o `OficinaMecanica.Seguranca`, que nao muda) conversam por
mensageria assincrona (RabbitMQ no AKS, via MassTransit) e coordenam o fluxo
transacional da OS com uma Saga **orquestrada**, com compensacao em toda etapa que
tem efeito colateral. Este RFC substitui em parte o RFC 0002 (eventos de dominio
in-process, sem broker) e o ADR 0001 (comunicacao so sincrona, sem mensageria), ambos
escritos quando nenhum outro servico precisava reagir a eventos da OS: a ausencia de
broker deixa de valer para a comunicacao **entre** servicos e segue valendo **dentro**
de cada servico (Domain Events in-process via MediatR). O que esses documentos decidem
alem disso nao muda: os Domain Events continuam sendo despachados in-process, e a
validacao do JWT continua sendo HTTP sincrono contra o JWKS do Seguranca.

## Motivacao
A oficina opera com varias filiais; o volume de ordens de servico e a interacao
entre atendimento, diagnostico, execucao e cobranca cresceram. No monolito, abrir
OS, aprovar orcamento e dar baixa no estoque acontecem numa unica transacao ACID,
o que esconde tres problemas: (1) nao existe "orcamento" nem "pagamento" como
conceito proprio - a OS em `AguardandoAprovacao` faz as vezes de orcamento e nao
ha cobranca; (2) "execucao" e so o status `EmExecucao`, sem fila nem responsavel;
(3) escalar ou falhar uma area derruba as demais. Separar em servicos independentes
(repositorio, banco e infraestrutura proprios) resolve isso, mas troca a transacao
ACID por consistencia distribuida - dai a Saga.

## Proposta
Fronteiras:
- **OS Service** (`OficinaMecanica`, Azure SQL `OficinaMecanicaDb`): Cliente,
  Veiculo, Servico, Peca (e estoque), OrdemDeServico e historico; hospeda a Saga.
- **Billing** (`OficinaMecanica.Billing`, Azure SQL `BillingDb`): Orcamento e
  Pagamento; integra o Mercado Pago (Checkout Pro, webhook assinado, estorno).
- **Execucao** (`OficinaMecanica.Execucao`, MongoDB Atlas): fila de producao,
  diagnostico, reparo, apontamentos e laudo.

Comunicacao: comandos (`Send`) e eventos (`Publish`) pelo RabbitMQ, com outbox/inbox
transacionais; REST sincrono so para consulta e para os pontos de entrada (cliente,
staff, mecanico, webhook do Mercado Pago). Nenhum servico le o banco de outro.

Saga: maquina de estados do MassTransit no OS Service, correlacionada pelo id da OS
(ver `docs/saga-maquina-de-estados.puml` e o ADR 0003). Fluxo feliz: OS aberta ->
diagnostico -> orcamento -> aprovacao do cliente (estoque reservado) -> pagamento ->
reparo -> OS finalizada. Compensacoes: orcamento recusado/expirado (cancela o orcamento),
pagamento nao concluido (cancela e libera estoque), execucao inviavel apos o
pagamento (estorno, depois libera estoque). A reserva de estoque so acontece na aprovacao do
cliente, entao recusa ou expiracao do orcamento so cancelam o orcamento (nao ha estoque a liberar).

Mudancas de API aceitas: aprovar/recusar orcamento passam a responder 202 (a decisao
e processada pela Saga); `PATCH /api/ordens-de-servico/{id}/status` passa a aceitar so
`Entregue`; OS criadas antes da Saga nao participam dela (409 nesses endpoints). Novos
status da OS: `AguardandoPagamento` (7) e `Cancelada` (8).

## Alternativas consideradas
- **Manter o monolito e so escalar horizontalmente**: nao resolve o acoplamento entre
  areas nem permite evoluir cobranca e producao de forma independente.
- **Saga coreografada**: ver ADR 0003.
- **Transacao distribuida (2PC/MSDTC)**: nao existe em Azure SQL serverless nem em
  MongoDB Atlas, e acoplaria a disponibilidade dos tres servicos.
- **Quebrar em mais de 3 servicos** (ex.: um servico so de estoque/catalogo): mais
  fronteiras do que o dominio exige hoje; estoque fica junto da OS porque a reserva
  precisa ser atomica com a decisao do cliente.

## Decisao
Tres microsservicos de negocio independentes (OS, Billing, Execucao), cada um com
repositorio, banco, pipeline e manifests proprios, coordenados por Saga orquestrada
sobre RabbitMQ/MassTransit. O RFC 0002 e o ADR 0001 ficam parcialmente substituidos:
a ausencia de broker deixa de valer para a comunicacao **entre** servicos; os Domain
Events in-process via MediatR (RFC 0002) continuam valendo **dentro** de cada servico
(e-mail e metricas), e o JWKS sincrono (ADR 0001) continua sendo como os servicos
validam o JWT do Seguranca.

## Documentos que detalham as decisoes
| Decisao | Documento | Quando e escrito |
|---|---|---|
| Saga orquestrada, MassTransit 8 gratuito, contratos por copia | ADR 0003, 0004, 0005 (este repositorio) | Junto com este RFC |
| Quebras de API e matriz sincrono x assincrono | ADR 0006 (este repositorio) | Junto com este RFC |
| Clean Architecture, DDD e CQRS via MediatR (seguem valendo dentro de cada servico) | RFC 0001 (este repositorio) | Anterior a este RFC; nao muda |
| Domain Events in-process sem broker e comunicacao so sincrona (substituidos so quanto a ausencia de broker entre servicos) | RFC 0002 e ADR 0001 (este repositorio) | Anteriores; o Status e a secao Atualizacao de cada um apontam para este RFC |
| Divisao em repositorios e stack .NET 8 / C# | RFC 0003 e RFC 0004 (este repositorio) | Anteriores; o RFC 0003 foi estendido por este (Billing e Execucao ganham repositorio) |
| Migrations do EF Core por step explicito no CI | ADR 0002 (este repositorio) | Anterior; o pipeline de 5 jobs do ADR 0009 o aplica, sem contradicao |
| CI/CD independente, gate de cobertura e Sonar, Trivy antes do push, prontidao | ADR 0009 (este repositorio) | Escrito junto com o pipeline completo do OS Service |
| Outbox/inbox transacional e publicacao antes do commit; sweeper de prazos e estados da saga | ADR 0007 e ADR 0008 (este repositorio) | Escritos depois de validar o desenho com a implementacao (F3a e F3b) |
| Mercado Pago (Checkout Pro, webhook, estorno) | ADR no repositorio Billing | Quando o Billing for implementado |
| MongoDB Atlas e fila com claim atomico | ADR no repositorio Execucao | Quando a Execucao for implementada |
| RabbitMQ no AKS, usuario de banco dedicado por servico | ADRs nos repositorios Infra e Banco | Quando a infraestrutura for descrita em Terraform |
| C4 nivel 3 (componentes do OS Service com a saga) | `docs/c4-nivel3-componente.puml` | Atualizado com a saga na F3b |
