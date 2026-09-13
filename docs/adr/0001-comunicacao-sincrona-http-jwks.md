# ADR 0001: Comunicacao sincrona via HTTP/JWKS (sem mensageria)

## Status
Aceito

## Contexto
O ecossistema hoje tem dois servicos independentes -
`OficinaMecanica` (monolito) e `OficinaMecanica.Seguranca` (autenticacao) -
que precisam se comunicar em runtime pelo menos uma vez: o monolito
precisa validar a assinatura dos tokens JWT emitidos pelo servico de
autenticacao, e pra isso busca as chaves publicas atuais via
`/.well-known/jwks.json`.

## Decisao
Essa comunicacao acontece via **HTTP sincrono simples** (o middleware de
autenticacao JWT do ASP.NET Core busca e faz cache das chaves JWKS
periodicamente) - nao ha nenhum barramento de eventos/mensageria (RabbitMQ,
Azure Service Bus, Kafka) entre os dois servicos, nem qualquer comunicacao
assincrona baseada em eventos. Dentro de cada servico, o MediatR ja cobre
o padrao CQRS (commands/queries in-process) - isso e ortogonal a essa
decisao, que trata apenas de comunicacao **entre** servicos diferentes.

## Consequencias
### Positivas
- Simplicidade: nenhuma infraestrutura de mensageria pra provisionar,
  operar ou monitorar.
- Modelo mental direto - "pra validar um token, busca a chave publica" e
  facil de entender e depurar (basta uma chamada HTTP).
- Nao ha necessidade real de consistencia eventual entre os dois servicos
  hoje - o unico dado compartilhado (chaves publicas JWKS) muda raramente
  e tolera cache com TTL.

### Negativas
- Acoplamento de disponibilidade em tempo de execucao: se
  `OficinaMecanica.Seguranca` estiver fora do ar e o cache local de JWKS
  do monolito expirar, novas validacoes de token podem falhar ate o
  servico voltar (mitigado pelo cache, mas nao eliminado).
- Se o ecossistema crescer e mais servicos precisarem reagir a eventos de
  negocio (ex: "ordem de servico criada" disparando notificacoes em outro
  servico), o modelo atual nao tem um mecanismo pronto de pub/sub entre
  processos - hoje esse tipo de reacao so existe **dentro** de cada
  servico via MediatR (`INotificationHandler`), nao entre servicos.

## Alternativas consideradas
- **Mensageria assincrona entre servicos (Azure Service Bus/RabbitMQ)**:
  se encaixaria melhor num cenario com mais servicos e mais eventos de
  negocio cruzando fronteiras de servico, mas seria complexidade
  desnecessaria para a unica integracao real que existe hoje (validacao
  de JWT via JWKS, que e naturalmente um padrao de consulta sincrona, nao
  um evento).
- **gRPC entre servicos**: mais eficiente que HTTP/JSON puro, mas o
  consumo de JWKS ja segue um formato padrao (RFC 7517) servido via HTTP -
  nao ha ganho real em trocar de protocolo para essa unica interacao.
