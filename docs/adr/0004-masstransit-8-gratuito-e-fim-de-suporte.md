# ADR 0004: MassTransit 8.x gratuito, versao fixada, e o fim de suporte da linha 8

## Status
Aceito

## Contexto
O MassTransit e a biblioteca de mensageria e saga mais usada no ecossistema .NET: traz
maquina de estados de saga, transporte RabbitMQ, transactional outbox/inbox para EF Core e
MongoDB e instrumentacao OpenTelemetry. A partir da versao 9 ele passou a exigir licenca
comercial; a linha 8 continua gratuita (Apache 2.0), mas sem suporte oficial apos 31/12/2026.
A ultima 8.x publicada e a `8.5.12`. O `MassTransit.EntityFrameworkCore 8.5.12` depende de
`Microsoft.EntityFrameworkCore.Relational 9.0.1` mesmo no alvo `net8.0`.

## Decisao
Usar o **MassTransit 8.x com versao exata** (`8.5.12`) em todos os pacotes `MassTransit*`
dos tres servicos, e nunca a linha 9. Guardrails:
- versoes sem curinga nos `.csproj`;
- o CI falha se algum pacote `MassTransit*` resolver para major diferente de 8;
- o Dependabot ignora atualizacoes major do MassTransit;
- o EF Core sobe para 9.0.x (alvo continua `net8.0`) nos servicos que usam o outbox EF.

## Consequencias
### Positivas
- Sem custo de licenca; comportamento conhecido e estavel para o escopo do projeto.
- Todo o desenho (saga, outbox, inbox, retry) usa APIs que existem na 8.x.

### Negativas
- Sem correcoes de seguranca oficiais apos 2026; a dependencia envelhece.
- Troca de biblioteca no futuro custa caro para sagas (state machines precisam ser
  reescritas); por isso o dominio e a Application nao dependem do MassTransit: o publish passa
  pela porta `IIntegrationEventPublisher` e a saga fica isolada na Infrastructure.

## Alternativas consideradas
- **MassTransit 9 (comercial)**: oficial e suportado, mas exige licenca paga em producao.
- **Rebus** (MIT): saga e outbox existem, ecossistema menor; viavel como rota de saida.
- **Wolverine** (MIT): as versoes atuais exigem .NET 9+, o ecossistema esta em .NET 8.
- **Implementacao propria sobre `RabbitMQ.Client`**: zero dependencia, mas muito codigo e risco
  em retry, DLQ, ordenacao e outbox.
- **Fork comunitario do MassTransit 8**: maturidade incerta.

Rota de saida documentada: se o suporte deixar de importar, migrar a Infrastructure de
mensageria para Rebus ou para o fork, sem tocar em Domain/Application.
