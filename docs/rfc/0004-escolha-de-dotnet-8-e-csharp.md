# RFC 0004: Escolha de .NET 8 / C# como stack do monolito

## Status
Aceito

## Resumo
`OficinaMecanica` e construido em .NET 8 (LTS) com C#, expondo uma Web API
via ASP.NET Core, e e a base tecnologica que os demais repositorios de
codigo do ecossistema (`OficinaMecanica.Seguranca`) tambem seguem.

## Motivacao
Precisavamos de uma stack que suportasse bem os padroes ja decididos
(Clean Architecture, DDD, CQRS via MediatR - ver RFC 0001 deste
repositorio), tivesse ORM maduro com suporte de primeira parte pra SQL
Server, gerasse artefato eficiente pra rodar em container (relevante dado
que o deploy final e via AKS, com recurso de cluster limitado - ver RFC
0003 do `OficinaMecanica.Infra`), e cujo ecossistema de bibliotecas
cobrisse as necessidades do projeto (mediator pattern, validacao,
documentacao OpenAPI, observabilidade via OpenTelemetry) sem exigir
integracoes caseiras.

## Proposta
.NET 8 (versao LTS mais recente no inicio do projeto) com C#, ASP.NET Core
pra Web API, Entity Framework Core como ORM (provider SQL Server),
MediatR pra CQRS, FluentValidation pra validacao de entrada, Swashbuckle
pra documentacao OpenAPI/Swagger. Mesma stack replicada em
`OficinaMecanica.Seguranca` (com Azure Functions Worker isolado no lugar
de ASP.NET Core, unica diferenca relevante de runtime).

## Alternativas consideradas
- **Node.js / TypeScript (ex: NestJS)**: tambem tem suporte solido a DDD/
  arquitetura em camadas e roda bem em container, mas o ecossistema Azure
  (Key Vault, Azure Functions, Application Insights/OpenTelemetry) tem
  integracao de primeira parte mais madura no .NET, e o projeto ja partia
  do pressuposto de usar Azure como nuvem (ver RFC 0001 do
  `OficinaMecanica.Infra`).
- **Java / Spring Boot**: stack igualmente capaz pra DDD/Clean
  Architecture e com ORM maduro (JPA/Hibernate), mas menor integracao
  nativa com Azure especificamente (Key Vault, Azure Functions) comparado
  ao .NET, que e desenvolvido pela mesma empresa que a nuvem escolhida.
- **.NET Framework (versao classica, nao .NET Core/5+)**: descartado
  cedo - sem suporte real a containers Linux, sem suporte oficial da
  Microsoft a longo prazo (.NET Framework esta em modo de manutencao),
  incompativel com o objetivo de rodar em AKS com imagem Linux.

## Decisao
.NET 8 (LTS) + C#, ASP.NET Core Web API, Entity Framework Core (SQL
Server), MediatR, FluentValidation - mesma stack replicada em
`OficinaMecanica.Seguranca` pra manter consistencia de convencoes entre
os repositorios de codigo do ecossistema.
