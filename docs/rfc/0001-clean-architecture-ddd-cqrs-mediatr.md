# RFC 0001: Clean Architecture + DDD + CQRS via MediatR

## Status
Aceito

## Resumo
O monolito (`OficinaMecanica`) adota Clean Architecture como organizacao
de camadas (Domain -> Application -> Infrastructure -> API, dependencias
sempre de fora para dentro), DDD para modelar o dominio (Aggregates,
Entities, Value Objects, Domain Events) e CQRS (Commands/Queries
separados) implementado via MediatR como mediator in-process.

## Motivacao
Uma oficina mecanica tem regras de negocio genuinamente complexas (ciclo
de vida de uma Ordem de Servico, aprovacao/recusa de orcamento, calculo de
pecas/servicos, historico de status) que nao se reduzem a CRUD simples.
Precisavamos de uma estrutura que:
- Isolasse essas regras de negocio de detalhes de infraestrutura (EF Core,
  Azure, HTTP), pra poder testar regra de negocio sem subir banco de dados
  nem servidor web.
- Escalasse em numero de casos de uso (dezenas de Commands/Queries) sem
  cada um virar um metodo a mais numa Controller/Service gigante.
- Deixasse explicito, so pelo nome da classe, se uma operacao le ou
  escreve dado (relevante inclusive pra decisoes de performance/cache
  futuras, mesmo sem bancos de leitura/escrita fisicamente separados
  hoje).

## Proposta
- **Clean Architecture**: 4 camadas (`Domain`, `Application`,
  `Infrastructure`, `API`), regra de dependencia unidirecional (camadas
  internas nunca referenciam as externas) - o `Domain` nao conhece nem EF
  Core nem ASP.NET Core.
- **DDD**: Aggregates (`Cliente`, `OrdemDeServico`, `Servico`, `Peca`) com
  suas proprias invariantes, Value Objects (`Documento`, `Placa`) pra
  validar formato/igualdade por valor em vez de identidade, Domain Events
  pra side effects que nao sao a responsabilidade central do agregado
  (ex: enviar e-mail quando o status muda).
- **CQRS via MediatR**: cada caso de uso vira um `Command` (escrita) ou
  `Query` (leitura) com seu proprio `Handler`, despachado via
  `IMediator.Send`. `ValidationBehavior` roda FluentValidation no pipeline
  do MediatR antes de qualquer handler executar, centralizando validacao
  de entrada sem precisar repetir `if (!ModelState.IsValid)` em cada
  Controller.

## Alternativas consideradas
- **Arquitetura em camadas simples (Controller -> Service -> Repository)**:
  mais familiar e rapida de comecar, mas tende a concentrar regra de
  negocio em Services genericos que crescem sem limite claro, e nao
  separa naturalmente leitura de escrita - a medida que o numero de casos
  de uso cresce, a tendencia e Services virarem "god classes".
- **Transaction Script (cada endpoint com sua propria logica procedural,
  sem camada de dominio rica)**: mais rapido pra CRUDs simples, mas nao se
  sustenta pras regras de negocio reais do dominio (ex: transicoes de
  status validas de uma Ordem de Servico, recalculo de valor total ao
  adicionar peca/servico) - a logica acabaria duplicada entre os
  endpoints que tocam a mesma entidade.
- **CQRS com Command/Query Bus externo (ex: um servico de mensageria
  dedicado, MassTransit)**: resolveria o mesmo problema de organizacao,
  mas adicionaria infraestrutura extra (broker, serializacao, retry) pra
  um cenario que, hoje, e inteiramente sincrono e dentro do mesmo
  processo - excesso de engenharia pro tamanho atual do sistema. Ver RFC
  0002 (mesmo repositorio) para a decisao especifica sobre Domain Events.

## Decisao
Clean Architecture (4 camadas) + DDD (Aggregates, Value Objects, Domain
Events) + CQRS via MediatR in-process, com `ValidationBehavior` no
pipeline pra validacao centralizada.
