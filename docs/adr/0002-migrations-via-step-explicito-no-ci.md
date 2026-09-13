# ADR 0002: Migrations do EF Core via step explicito no CI

## Status
Aceito

## Contexto
`Program.cs` sempre teve um bloco condicional (`if
(app.Environment.IsDevelopment())`) em torno da chamada de migracao
automatica do Entity Framework Core (`db.Database.Migrate()`) - a ideia
original era que migracoes automaticas no startup so deveriam acontecer
em ambiente de desenvolvimento, nunca em producao (rodar migracao de
schema toda vez que um Pod sobe, sem controle explicito de quando isso
acontece, e um padrao arriscado em producao: pode rodar em paralelo se
subir mais de um Pod ao mesmo tempo, e nao ha oportunidade de revisar o
que vai mudar antes de acontecer).

Na pratica, isso nao estava funcionando como pretendido: a variavel de
ambiente `ASPNETCORE_ENVIRONMENT` no manifesto do Kubernetes
(`k8s/oficinamecanica-api/deployment.yaml`) estava fixa em `"Development"`
mesmo no deploy de producao no AKS - um bug real, nao uma escolha - o que
significava que a migracao automatica **estava** rodando em producao a
cada novo Pod, por acidente.

## Decisao
Duas mudancas juntas: primeiro, corrigir o bug de verdade
(`ASPNETCORE_ENVIRONMENT` passa a ser `"Production"` no deploy do AKS,
fazendo o gate em `Program.cs` funcionar como sempre deveria). Segundo,
como isso significa que a migracao automatica no startup deixa de rodar
em qualquer ambiente real, adicionar um **step explicito** no pipeline de
CI/CD (`dotnet ef database update`, buscando a connection string do Key
Vault) que roda antes do deploy da nova imagem - a migracao passa a
acontecer de forma controlada, uma vez por deploy, nunca em paralelo por
multiplos Pods subindo ao mesmo tempo.

## Consequencias
### Positivas
- Migracao de schema roda exatamente uma vez por deploy, de forma
  previsivel, nao uma vez por Pod que sobe.
- Corrige um bug real de configuracao que fazia producao se comportar como
  ambiente de desenvolvimento nesse aspecto especifico.
- Fica visivel no log do pipeline de CI/CD quando uma migracao roda e se
  ela teve sucesso, em vez de escondida no log de inicializacao de um Pod.

### Negativas
- Mais um step no pipeline que pode falhar e bloquear o deploy (embora
  isso seja o comportamento desejado - preferimos falhar cedo a subir uma
  imagem nova contra um schema desatualizado).
- O pipeline de CI/CD agora precisa de permissao para ler o secret da
  connection string do Key Vault (antes, so a aplicacao em si precisava).

## Alternativas consideradas
- **Manter migracao automatica no startup, so corrigindo o
  `ASPNETCORE_ENVIRONMENT`**: reintroduziria o risco original que o gate
  tentava evitar (migracoes concorrentes se mais de um Pod subir ao mesmo
  tempo durante um rollout).
- **Job/Init Container dedicado no Kubernetes pra rodar a migracao**:
  resolveria o problema de concorrencia sem sair do cluster, mas exigiria
  outro manifesto e imagem, quando o pipeline de CI/CD ja tem acesso ao
  codigo/dotnet-ef e ao Key Vault - optou-se por manter a migracao no
  proprio pipeline por simplicidade.
