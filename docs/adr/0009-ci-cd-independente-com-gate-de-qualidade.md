# ADR 0009: CI/CD independente com gate de qualidade

## Status
Aceito

## Contexto
O OS Service (este repositorio, que nasceu como monolito) e o orquestrador da Saga e um dos tres servicos independentes do
ecossistema: repositorio, banco (`OficinaMecanicaDb`), identidade OIDC e rota (`oficinaserver`) proprios. O pipeline herdado
tinha tres jobs (build com testes sem cobertura, push da imagem e deploy) e nenhuma das garantias que o Billing e a Execucao
ja tem: gate de cobertura, qualidade estatica, scan da imagem antes do push, testes de infraestrutura com SQL Server e
RabbitMQ reais, validacao dos manifests, contrato OpenAPI versionado e manifests do Kubernetes endurecidos, com probes de
prontidao. Este ADR leva o OS Service ao mesmo desenho do ADR 0003 do Billing; o que e especifico deste servico fica isolado
(bloco `env` do `ci.yml`, variaveis no topo de `scripts/`, `k8s/` e `Dockerfile`).

Restricoes que moldam as escolhas: o MassTransit 9.x e comercial (o ecossistema fica no 8.x, ADR 0004); o cluster tem um
unico no sem CPU de requests sobrando; o Azure SQL e serverless (pausa por inatividade); `main` (producao) e `release`
(homologacao) fazem deploy no MESMO Deployment e banco, pelo mesmo motivo do no unico. O workflow novo ainda nao rodou no
GitHub: foi validado localmente (`actionlint`, `shellcheck`, `kubeconform`, `docker build`, execucao dos scripts e dos testes
com Docker) e a primeira execucao real acontece na publicacao.

## Decisao
**Cinco jobs encadeados em `.github/workflows/ci.yml`:**
- `build-and-test` (`Build & Test`): restore, build Release, testes dos scripts de CI, guardrail do MassTransit, validacao dos
  manifests, conferencia do OpenAPI, testes `Category!=Infra`, testes `Category=Infra` (Testcontainers; o runner ubuntu ja traz
  Docker), cobertura mesclada com gate e publicacao dos resultados (`dorny/test-reporter`, Job Summary e artifacts).
- `quality` (`Quality`): SonarCloud, depende do job anterior.
- `scan-image` (`Scan Image (Trivy)`): so `push`/`workflow_dispatch` em `main` e `release`; PR nunca publica imagem. Roda com
  `contents: read` SOMENTE: constroi a imagem, escaneia com o Trivy e salva a imagem aprovada como artifact.
- `build-and-push-image`: com `id-token: write`; baixa o artifact, `docker load`, login OIDC e push da MESMA imagem.
- `deploy-to-aks`: mesma condicao; migrations, `kubectl apply`, `kubectl set image`, `rollout status`; com
  `concurrency: deploy-aks-oficinamecanica-api` (`cancel-in-progress: false`), porque `main` e `release` implantam no mesmo
  Deployment e dois deploys simultaneos rodariam migrations e rollouts ao mesmo tempo.

**Gate de cobertura de 80% sobre o numero mesclado.** `scripts/cobertura.sh` coleta OpenCover (coverlet) e `.trx` em cada
projeto de teste na mesma execucao (os testes rodam uma unica vez) e une tudo com o ReportGenerator, inclusive os testes
de infraestrutura. O gate (`scripts/verificar-cobertura.sh`) vale para o numero "com exclusoes", que tira apenas o que o
`CLAUDE.md` documenta (`Migrations`, `Program.cs`, `DependencyInjection*` e as EF Configurations em `Data/Configurations`); o
numero bruto e publicado ao lado. O limite e `COBERTURA_MINIMA` (padrao 80) e o script falha com codigo de saida diferente
de zero abaixo dele. O percentual sai da contagem de linhas cobertas e cobertaveis, nao da linha "Line coverage" (arredondada
para uma casa: 79,95% apareceria como 80% e passaria). A mesma lista de exclusoes alimenta o Sonar
(`sonar.coverage.exclusions`). O atributo `[ExcludeFromCodeCoverage]` e honrado pelo coverlet e some das duas medicoes sem
deixar rastro no gate; por isso o uso e proibido sem justificativa em ADR e o `cobertura.sh` lista as ocorrencias no
relatorio. Os projetos de teste sao `Contracts.Tests`, `Domain.Tests`, `Application.Tests`, `Integration.Tests` e
`Infrastructure.Tests` (os `Category=Infra` moram hoje no ultimo); a fase `testes-sem-infra` roda todos com `Category!=Infra` e
`testes-infra` roda os MESMOS cinco com `Category=Infra` (nos projetos sem teste desse tipo o filtro nao acha nada e o `dotnet test`
sai com 0), a partir de uma unica lista de projetos no `cobertura.sh`.

**Teste preso nao pode travar o pipeline.** Um teste que espera uma tarefa que o codigo sob teste deixou de liberar nao
reprova: fica pendurado, e o run so terminaria no `timeout-minutes` do job (e, no `ci-local.sh`, nunca). Duas defesas: os testes
que esperam tarefa controlada por eles (health checks com `TaskCompletionSource`) limitam a espera com `AguardarAsync` (10 s) e
falham com a explicacao do que deveria ter acontecido; e o `cobertura.sh` roda `dotnet test` com
`--blame-hang-timeout 5m --blame-hang-dump-type none`, que encerra o testhost de qualquer teste parado por 5 minutos sem
gravar despejo de memoria em `TestResults`.

**Os scripts de decisao tem testes.** `scripts/testar-scripts.sh` (no `Build & Test` e no `ci-local.sh`) exercita o gate e o
guardrail com entradas sinteticas, inclusive os caminhos de reprovacao (79,9% reprova, 80,0% aprova, numero ausente nao
aprova, `COBERTURA_MINIMA` vazio cai em 80, MassTransit 9.x direto ou transitivo reprova, `MassTransit.EntityFrameworkCore` 9.x
reprova), tambem a separacao de privilegios do proprio `ci.yml` (ver abaixo), e planta defeitos em copias dos scripts (gate que
sai 0, minimo padrao zerado, guardrail que aceita o major 9, verificador do workflow que libera `aquasecurity/*`, entre
outros), exigindo que cada um seja detectado. Sem isso, o gate e o guardrail so seriam vistos passando num CI saudavel, e um
defeito que os tornasse inofensivos nunca apareceria. Testar o `verificar-cobertura.sh` isolado nao basta: se alguem apagar a
chamada do gate em `cobertura.sh`, o CI deixa de reprovar cobertura e nenhum caso do gate percebe. Por isso o
`cobertura.sh relatorio` tambem roda de ponta a ponta sobre um OpenCover sintetico (79% reprova, 81% aprova, o numero vem "com
exclusoes", a variavel `COBERTURA_MINIMA` chega ao gate, fase invalida e relatorio sem OpenCover saem com 2), o
`validar-k8s.sh` roda o kubeconform sobre manifests sinteticos (campo desconhecido reprova por causa do `-strict`; o CRD sem
schema e ignorado; um manifest invalido na segunda pasta da lista reprova), o `exportar-swagger.sh --verificar` roda com um
`dotnet` de mentira que grava um documento sintetico (em dia aprova, divergente ou sem `docs/openapi.json` reprova, argumento
invalido sai com 2), e se confere que `ci.yml` e `ci-local.sh` chamam os mesmos passos e que o `ci.yml` exporta `COBERTURA_MINIMA: "80"` (baixar o valor
no workflow deixaria o gate aprovando quase tudo sem reprovar nenhum outro caso; o mutante troca 80 por 10 numa copia do
`ci.yml`). Um `dotnet` de mentira registra as chamadas do `cobertura.sh` e se confere que as fases `testes-sem-infra` e `testes-infra` rodam
os MESMOS projetos de `tests/*.Tests` (com `Category!=Infra` e `Category=Infra`): um teste de infraestrutura criado fora do
`Infrastructure.Tests` nao fica sem fase. Cada script tem mutantes proprios (a
chamada do gate apagada, o gate lendo o numero bruto, cada filtro de exclusao removido, `-strict` retirado, falha do
kubeconform ignorada, so a primeira pasta validada...). Os casos que dependem do Docker (kubeconform) sao pulados com aviso
quando ele nao esta ligado. Na ultima execucao completa foram 76 casos e 38 mutantes detectados; na medicao de cobertura de
2026-11-08 (`cobertura.sh` completo, com o BDD nas fases de teste e o gate aprovado) foram 1147 testes (Contracts 71, Domain 153,
Application 306, Integration 176, Infrastructure 429, dos quais 75 `Category=Infra`, e Bdd 12) e cobertura de linhas de 96,71% bruta
(6284/6498) e 95,85% com as exclusoes (3395/3542); o registro por assembly esta em `docs/relatorios/cobertura.md`.

**SonarCloud com Quality Gate bloqueante.** O job `quality` baixa o OpenCover do job anterior (artifact), roda
`dotnet-sonarscanner begin/build/end` e usa `sonar.qualitygate.wait=true`: se o Quality Gate reprova, o job falha. Projeto e
organizacao vem de variaveis do repositorio (`vars.SONAR_PROJECT_KEY`, `vars.SONAR_ORGANIZATION`) e o `SONAR_TOKEN` de
`secrets`; e o unico secret de longa duracao do pipeline, porque o SonarCloud nao suporta OIDC. Sem token o job falha com
mensagem clara; em PR de fork (sem acesso a secrets) o job e pulado. Migrations e o script de carga (`stress/`) ficam fora da
analise.

**Trivy antes do push, em job sem OIDC.** O job `scan-image` constroi com `load: true` (sem push), escaneia com
`aquasecurity/trivy-action` (falha em `HIGH`/`CRITICAL` com correcao disponivel, `ignore-unfixed`) e salva a imagem aprovada
(`docker save`) como artifact. O job `build-and-push-image` so baixa o artifact, carrega e empurra a MESMA imagem que foi
escaneada. A separacao existe porque o Trivy e as actions do Docker sao de terceiros e o job de push tem `id-token: write`:
qualquer step de um job com essa permissao pode pedir o token OIDC, e a identidade do servico tem `AcrPush`, `AKS Cluster
Admin` e leitura do Key Vault. Com a divisao, o job que executa codigo de terceiros so tem `contents: read` e o que tem OIDC
so executa actions do GitHub/Microsoft. A tag de deploy e sempre o sha do commit; `:latest` so sai da `main`, para um push em
`release` nao sobrescrever o que esta em producao.

**A separacao de privilegios tem guarda automatica.** `scripts/verificar-workflow.sh` le o `ci.yml` (ignorando comentarios) e
reprova se um job que declara `id-token` executar qualquer action fora de `actions/*` e `azure/*`, ou se `id-token`/`write-all`
for concedido no nivel do workflow (valeria para todos os jobs). O `testar-scripts.sh` o roda contra o `ci.yml` real e contra
workflows sinteticos de falha (o job de push com a action do Trivy, com a action entre aspas, `id-token` no `scan-image`, CRLF)
e contra mutantes do proprio verificador. Sem essa guarda, uma action de terceiros colocada de volta no job de push passaria
despercebida: a separacao so existia como comentario e como desenho do `ci.yml`.

**Guardrail do MassTransit.** `scripts/verificar-masstransit.sh` roda `dotnet list package --include-transitive` e falha se
qualquer pacote `MassTransit*`, direto ou transitivo, resolver para major diferente de 8. O Dependabot tambem ignora o
major do `MassTransit*` (e o major do EF Core, que continua no 9.x por causa do `net8.0` e do
`MassTransit.EntityFrameworkCore 8.5.12`).

**OIDC.** `azure/login` com a identidade do OS Service (modulo `github_oidc` do `OficinaMecanica.Infra`). Os jobs que falam com
o Azure declaram `id-token: write`; os demais ficam com `contents: read`. Todo `actions/checkout` usa
`persist-credentials: false`.

**Actions por SHA (aplicado em 2026-11-09).** Todas as actions do `ci.yml`, de terceiros (`docker/*`, `aquasecurity/trivy-action`,
`dorny/test-reporter`) e do GitHub/Microsoft (`actions/*`, `azure/*`), estao fixadas pelo SHA completo do commit, com a versao num
comentario ao lado (`uses: actions/checkout@<sha> # v4.4.0`). Uma tag e mutavel: quem controla a action pode move-la para outro
codigo; o SHA nao. O SHA veio da tag que o workflow ja usava, resolvida pela API do GitHub para o commit em que ela apontava no dia,
e a versao do comentario e a tag semver mais especifica que aponta para o mesmo commit (no `dorny/test-reporter` e `v1`). O pin
nao audita o conteudo: impede que a tag seja movida depois, mas nao prova que o commit de hoje e confiavel. O Trivy estava
referenciado como `0.28.0`, tag que nao existe no repositorio da action (a real e `v0.28.0`); o pin aponta hoje para a `v0.36.0`: a `v0.28.0` chama internamente `setup-trivy@v0.2.1`, tag que deixou de existir, e a `v0.36.0` fixa as actions internas por SHA.
O Dependabot de `github-actions` propoe as atualizacoes mantendo o formato `@sha # versao`.

**Migrations no deploy.** O job `deploy-to-aks` le a connection string do Key Vault (`kvfiap`, segredo
`sql-connection-string`) na mesma sessao OIDC e roda `dotnet ef database update` antes do rollout, com ate 5 tentativas por
causa do Azure SQL serverless. A ferramenta vem de `dotnet-tools.json` (`dotnet-ef 9.0.22`, a mesma linha do EF Core do
projeto); o passo anterior instalava `dotnet-ef 8.*`, incompativel com o EF Core 9 que o `MassTransit.EntityFrameworkCore`
exige. A API em `Production` nunca migra sozinha (so em `Development`). E a decisao do ADR 0002 (migrations por step
explicito no CI, com `ASPNETCORE_ENVIRONMENT=Production` no manifesto); este pipeline a aplica no job `deploy-to-aks`.

**Ruleset versionado e `Quality` obrigatorio.** `.github/rulesets/protect-main-release.json` replica a protecao ja existente
(`main` e `release`: sem delete nem force-push, PR obrigatorio) e exige os checks `Build & Test` e `Quality`. Nao ha script
para aplica-lo: o JSON versionado e a entrada direta da GitHub CLI (`gh api --input`), executada a mao por quem administra o
repositorio, e nada neste repositorio a dispara. O ruleset `protect-main-release` **ja existe e esta ativo** no GitHub exigindo
so o `Build & Test` (conferido so em leitura em 2026-11-09), entao o comando e de atualizacao (`PUT`), nao de criacao:

```bash
gh api --method PUT repos/willianrmattos/OficinaMecanica/rulesets/$(gh api repos/willianrmattos/OficinaMecanica/rulesets --jq '.[] | select(.name == "protect-main-release") | .id') --input .github/rulesets/protect-main-release.json
```

A ordem importa: so atualizar depois de o SonarCloud estar configurado (`SONAR_TOKEN` e as variaveis `SONAR_*`) e de o job
`Quality` ter rodado ao menos uma vez. Antes disso o check obrigatorio nao existe ou falha, e todo PR fica bloqueado esperando
por ele.

**OpenAPI versionado.** `docs/openapi.json` e gerado pela API compilada (`swagger tofile`, ferramenta fixada em
`dotnet-tools.json`) e o CI falha se ele divergir da API; o arquivo tem fim de linha LF fixo (`.gitattributes`).

**Manifests.** `k8s/oficinamecanica-api/` com um unico placeholder, `${IMAGE}`, renderizado por `envsubst` com a imagem do sha
antes do `kubectl apply` (o Deployment nasce com a tag certa e nenhum apply regride a imagem); `k8s/mailpit/` e aplicado como
esta (sem placeholder), com a versao da imagem fixada. Segredos so por SecretProviderClass/Key Vault (`sql-connection-string`,
`rabbitmq-username`, `rabbitmq-password`); nada literal nos manifests. Endurecimento: usuario nao-root (UID 1654 do `app` da
imagem aspnet), `readOnlyRootFilesystem` com `emptyDir` em `/tmp`, `drop: ["ALL"]`, `seccompProfile: RuntimeDefault`,
`automountServiceAccountToken: false`. O Ingress segue como catch-all em `/` (Billing e Execucao entram por prefixo). O
Deployment declara explicitamente `Mensageria__Transporte`, `Mensageria__Persistencia`, `RabbitMq__*`, `Saga__*` e
`Execucao__BaseUrl` (Service da Execucao, `http://oficinamecanica-execucao.default.svc.cluster.local:80`, HTTP interno).
`strategy.maxSurge: 0` por causa do no unico; o HPA passou de 2-5 para 1-2 replicas pelo mesmo motivo, e os requests/limits do
monolito (100m/256Mi, 500m/512Mi) foram mantidos: medido no container, a API usa cerca de 95 MiB em repouso e 125 MiB depois
de 400 requisicoes, mas a CPU sob mensagens reais da saga nao foi medida e nao ha evidencia para reduzir.

**Orcamento de CPU e memoria do no (ESTIMATIVA, nao medida).** Nao rodei `kubectl describe node` (o contexto local aponta para o
cluster real), entao os numeros abaixo combinam o que os repositorios declaram com a formula de reserva documentada do AKS, e
precisam ser conferidos contra o `Allocatable` real antes do primeiro deploy. `Standard_D4as_v4` tem 4 vCPU e 16 GiB; descontada a
reserva do AKS (estimada em ~80m de CPU e ~2,7 GiB de memoria), o alocavel fica perto de **3.900m e 13 GiB**. Requests declarados nos
manifests e no Terraform, com os HPAs no piso e no teto:

| Carga | Request por pod | Piso (1 replica) | Teto (HPA em 2) |
|---|---|---|---|
| OS Service (este) | 100m / 256Mi | 100m / 256Mi | 200m / 512Mi |
| Billing | 50m / 192Mi | 50m / 192Mi | 100m / 384Mi |
| Execucao | 50m / 192Mi | 50m / 192Mi | 100m / 384Mi |
| Mailpit | 10m / 32Mi | 10m / 32Mi | 10m / 32Mi |
| RabbitMQ (StatefulSet, `OficinaMecanica.Infra`) | 100m / 256Mi | 100m / 256Mi | 100m / 256Mi |
| OTel Collector (`OficinaMecanica.Infra`) | 50m / 128Mi | 50m / 128Mi | 50m / 128Mi |
| **Total conhecido** | | **360m / ~1,0 GiB** | **560m / ~1,7 GiB** |

Ficam de fora os pods de sistema do AKS (`kube-system`), o `ingress-nginx` e o `nri-bundle` da New Relic, cujos requests vem dos
valores padrao dos charts e nao estao versionados; e justamente essa parcela, que nao sei medir daqui, que o dono do cluster
descreve como o que deixa o no sem CPU sobrando. Pelo teto conhecido, o piso e o teto antigos do HPA deste servico (2 a 5 replicas
de 100m) somariam 200m a 500m so aqui, contra 100m a 200m agora: no teto, o conjunto conhecido cairia de ~860m para ~560m, uma
folga de ~300m de CPU de request que e a que importa para o `maxSurge: 0`. A memoria nao e a restricao (o teto conhecido e uma
fracao do alocavel); a CPU de request e.

**Prontidao: banco derruba, broker so rebaixa.** `/health` e liveness puro (200 sempre, sem dependencias: uma queda de
dependencia nao pode reiniciar o pod) e `/health/ready` roda os checks da tag `ready`:
- `sqlserver` falha como `Unhealthy` e a resposta e **503**: sem o banco a API nao aceita escrita (o outbox mora nele), entao o
  pod sai de rotacao.
- `rabbitmq` (conexao AMQP de verdade, so com `Mensageria__Transporte=RabbitMq`) e `masstransit-bus` falham como `Degraded` e a
  resposta continua **200**: com o outbox transacional a API segue aceitando escrita e a mensagem espera no banco, entao tirar
  o pod de rotacao por causa do broker transformaria uma queda do RabbitMQ em indisponibilidade total. O `Degraded` aparece no
  corpo JSON, que traz so nome, status e descricao fixa (nunca a excecao, que carrega host, porta e credencial).
- Cada check desiste em 3 s, inclusive contra um servidor que aceita a TCP e nao responde (o cliente AMQP sozinho leva ~8 s,
  por isso o `WaitAsync` e o fechamento da conexao que completar depois do limite), para caber no `timeoutSeconds: 5` da
  readiness. O cancelamento do chamador (probe abortada) sobe como excecao em vez de virar `Degraded`/`Unhealthy`, para nao
  gravar uma medicao falsa no cache. `periodSeconds: 30` so limita a carga das probes; o auto-pause do Azure SQL serverless nao
  e evitado por elas (o outbox e o sweeper consultam o banco a todo momento).
- **Cache de 5 s** (`Health__ProntidaoCacheSegundos`; 0 desliga e so os testes de infraestrutura usam) nos resultados de
  `sqlserver` e `rabbitmq`: a rota `oficinaserver` da APIM repassa qualquer path, entao `/health/ready` e anonimo e
  alcancavel da internet, e sem cache cada requisicao abriria uma conexao SQL e uma AMQP (vetor de esgotamento de conexoes).
  Com o cache o custo e de 1 conexao SQL + 1 AMQP a cada 5 s, qualquer que seja a taxa de requisicoes; ele e menor que o
  `periodSeconds`, entao cada probe do kubelet ainda ve uma medicao nova. Requisicoes simultaneas esperam uma unica avaliacao,
  e excecao/cancelamento nunca e guardado.
- O `startupProbe` em `/health` aceita 120 s (24 x 5 s): o Kestrel so abre a porta depois de o MassTransit subir (ate 60 s com
  o broker lento).

## Consequencias
### Positivas
- Nenhum merge em `main`/`release` sem build, testes de infraestrutura reais, cobertura >= 80% e Quality Gate aprovado.
- O que o Trivy aprova e exatamente o que vai para o ACR; nenhuma imagem com vulnerabilidade corrigivel e publicada.
- O gate de cobertura existe no repositorio (script) e no Sonar: uma falha de configuracao do SaaS nao deixa o limite sem
  guarda.
- Nenhum segredo de longa duracao do Azure; o pipeline de um servico nao consegue agir com a identidade de outro.
- `scripts/ci-local.sh` reproduz o job `Build & Test` na maquina do desenvolvedor, com os mesmos scripts.
- O contrato publico fica versionado (`docs/openapi.json`) e muda junto com o PR que altera a API.
- O pod roda sem root, sem capabilities e com o sistema de arquivos somente leitura; testado com a imagem real, SQL Server e
  RabbitMQ reais (a API sobe, `/health/ready` responde 200 saudavel, 200 `Degraded` com o broker parado e 503 com o banco offline).

### Negativas
- Testes de infraestrutura baixam imagens grandes (SQL Server) e tornam o `Build & Test` mais lento (cerca de 9 minutos no
  `ci-local.sh`); o job `quality` compila de novo porque o scanner precisa instrumentar o build.
- O check `Quality` pulado (PR de fork) conta como satisfeito para o GitHub: PR de fork nao passa pelo Sonar e depende de
  revisao.
- A migration roda com a versao antiga ainda no ar, e rollback de imagem nao desfaz migration: as migrations precisam ser
  compativeis com a versao anterior (expandir antes de contrair).
- Com uma replica e `maxSurge: 0` ha uma janela sem pod a cada deploy (ate o `startupProbe` passar): as chamadas HTTP dessa
  janela falham; os comandos e eventos da saga esperam no RabbitMQ e o sweeper retoma sozinho. Reduzir o piso do HPA de 2 para
  1 tirou a redundancia que o monolito tinha; a causa e o no unico sem CPU de requests sobrando.
- O runner do GitHub precisa alcancar o Azure SQL (regra de firewall do servidor), a mesma premissa do pipeline anterior.
- Antes da publicacao o workflow so foi validado estaticamente e pelos scripts, e as actions estao fixadas por SHA sem que o
  conteudo de cada commit fixado tenha sido auditado (ver a decisao "Actions por SHA").
- Desvio do plano (secao 8, "testes de Infra em job separado"): os testes de Infra rodam num step proprio do MESMO job
  `Build & Test`. O gate de cobertura exige o OpenCover de todos os projetos no mesmo workspace; um job de Infra reconstruiria
  a solucao, transferiria artifacts e obrigaria um terceiro job para unir e aplicar o gate, sem ganho de isolamento (o runner e
  o mesmo), e os nomes dos checks obrigatorios ficariam menos estaveis. O resultado de cada grupo continua separado por step,
  `.trx` e `dorny/test-reporter`. Se o tempo do `Build & Test` virar problema, a separacao e o proximo passo.
- O `ci.yml` so tem testes para a separacao de privilegios (`verificar-workflow.sh`), para o valor do gate de cobertura exportado
  (`COBERTURA_MINIMA: "80"`) e o `actionlint`: a ordem dos jobs e as condicoes `if` so se provam na primeira execucao real.
- O orcamento de CPU e memoria do no e uma estimativa: falta conferir o `Allocatable` real e os requests dos pods de sistema, do
  `ingress-nginx` e da New Relic (`kubectl describe node`) antes do primeiro deploy.
- **Broker fora nao tira o pod de rotacao**: com o RabbitMQ parado o `/health/ready` segue 200 e so o corpo mostra `rabbitmq`
  `Degraded`; quem monitora so sabe pelo corpo. O alerta (e um gauge de mensagens pendentes no outbox) ainda nao existe como
  metrica. Em contrapartida, banco fora derruba a prontidao (503) e o pod sai de rotacao ate o banco voltar; com o Azure SQL
  serverless acordando, a probe pode ficar em 503 por alguns ciclos.
- **`/health/ready` anonimo exposto pela APIM**: a rota repassa qualquer path, entao qualquer cliente da internet pode chamar
  `/oficinaserver/health/ready`. O cache de 5 s limita o custo a uma conexao SQL e uma AMQP a cada 5 s e o corpo nao vaza
  excecao, mas o endpoint continua publico. Mitigacao no `OficinaMecanica.Infra` (ADR 0007 de la): a policy da APIM que devolve
  404 para `/health/ready` e `/metrics` na rota publica existe e fica ativa quando o Terraform do Infra for aplicado (ainda nao
  foi); a probe do kubelet bate direto no pod e nunca passa pela APIM.
- O `Mailpit` do cluster continua sendo SMTP de demonstracao e a UI dele fica exposta por host `nip.io`; nao e canal de
  e-mail de producao.

## Alternativas consideradas
- **Pipeline unico para todos os servicos**: acopla os repositorios e contraria o requisito de CI/CD independente.
- **Rodar os testes duas vezes** (um step de testes e outro para cobertura): dobra o tempo, principalmente com os testes de
  infraestrutura; a coleta junto do teste entrega o mesmo numero.
- **Gate de cobertura so no SonarCloud**: depende de configuracao externa; o script no repositorio mantem o limite
  verificavel localmente e no CI.
- **Trivy depois do push**: a imagem vulneravel ja estaria no registry quando o scan reprovasse.
- **Scan e push no mesmo job**: mais simples, mas o job com `id-token: write` executaria o Trivy e as actions do Docker, de
  terceiros, com acesso possivel ao token OIDC (e, por ele, a `AcrPush`, `AKS Cluster Admin` e ao Key Vault).
- **Job `Infra Tests` separado**: ver o desvio na secao de consequencias; custa um rebuild, artifacts e um terceiro job para o
  gate, e nao isola nada alem do log.
- **Reconstruir a imagem para o push** (segundo `build-push-action`): o cache costuma reproduzir as camadas, mas nao ha
  garantia de que sejam identicas as escaneadas.
- **Migration na partida da API** (`Database.Migrate()` em `Production`): varias replicas disputariam o esquema e uma
  replica nova alteraria o banco por conta propria.
- **Migration como Job/initContainer no cluster**: exige a connection string de escrita DDL dentro do cluster e mais pecas;
  o CI ja tem a identidade e o acesso ao secret. O `dotnet ef migrations bundle` e uma evolucao possivel.
- **Manifest com `:latest` mais `kubectl set image`** (como o pipeline anterior): cada apply voltaria o Deployment para
  `latest` e o `set image` disparava um segundo rollout, com duas indisponibilidades seguidas no no unico.
- **Manter o HPA em 2-5 replicas**: no teto, o OS Service sozinho pediria 500m de CPU de request (contra 200m com 1-2 replicas) num no
  que, segundo o dono do cluster, ja nao tem CPU sobrando para os tres servicos mais a plataforma (ver o orcamento estimado
  na Decisao).
- **`maxSurge: 1` com `maxUnavailable: 0`** (rollout sem janela, com 1 replica): o pod novo sobe antes de o antigo sair, entao nao
  ha indisponibilidade no deploy. Nao foi escolhida porque exige os requests de um segundo pod (100m de CPU, 256Mi) ao mesmo
  tempo, e num no sem CPU sobrando o pod surge fica `Pending` por `Insufficient cpu` e o `rollout status` do CI nunca termina:
  foi exatamente o que o Deployment anterior fazia com o `maxSurge` padrao de 25% e motivou o `maxSurge: 0`. Com a estimativa de
  orcamento acima (~300m de folga de CPU de request no teto) essa opcao provavelmente cabe; a decisao deve ser revista depois de
  conferir o `Allocatable` e os requests reais do no, porque ela elimina a janela sem pod a cada deploy.
- **Service principal com client secret em vez de OIDC**: segredo de longa duracao para rotacionar e vazar.
- **Broker fora como `Unhealthy` (503)**: o pod sairia de rotacao em toda queda do RabbitMQ e a API deixaria de aceitar escrita
  que o outbox absorveria sem perda.
- **`/health/ready` sem cache**: cada requisicao anonima abriria uma conexao SQL e uma AMQP; o cache de 5 s limita o custo sem
  esconder a queda por mais que o `periodSeconds` das probes.
- **Readiness com o limite padrao de 1 s**: os checks levam ate 3 s, entao a probe falharia sem o banco ou o broker estarem fora.
