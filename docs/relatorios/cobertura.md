# Evidencia de cobertura de testes

Registro da cobertura de linhas e da contagem de testes do **OS Service**
(`OficinaMecanica`). Os numeros sao os da medicao mais recente, feita com `bash scripts/cobertura.sh`
completo (gate aprovado) e lidos dos relatorios gerados
(`TestResults/report-bruto/Summary.txt` e `TestResults/report-com-exclusoes/Summary.txt`) e dos
arquivos `.trx` de cada projeto de teste.

| Item | Valor |
|------|-------|
| Data da medicao | **2026-11-08** (testes entre 19:14 e 19:22 e relatorios gerados as 19:22, horario local da maquina de desenvolvimento, UTC-4) |
| Como foi gerado | `bash scripts/cobertura.sh` completo (fase `tudo`: testes sem Docker, testes de infraestrutura com Docker, relatorio e gate), com o gate aprovado |
| Codigo medido | `src/` e `tests/` deste repositorio com o historico de status da OS em ordem cronologica, o tratamento do abandono do cliente no middleware de excecoes e o `UseHttpMetrics` por fora dele. O arquivo mais recente de `src/`, `tests/` e `scripts/` foi modificado as 19:12:17, antes do inicio da medicao (19:14); este documento, o `README.md`, o `CLAUDE.md` e o ADR 0009 sao editados depois e nao entram na medicao |
| Resultado dos testes | 1147 executados, 1147 aprovados, 0 falhas |
| Gate do CI | cobertura de linhas **>= 80%** sobre o numero "com exclusoes" |

> Esta medicao foi feita **localmente**. O CI no GitHub Actions ainda nao rodou com esta
> versao (nada foi publicado); a evidencia equivalente do CI so existe depois da primeira
> execucao (ver [Onde fica no CI](#onde-fica-no-ci)).

## Resumo

| Medicao | Linhas cobertas / cobertaveis | Cobertura |
|---------|-------------------------------|-----------|
| **Bruta** (conta tudo) | 6284 / 6498 | **96,71%** |
| **Com exclusoes** (valida o gate) | 3395 / 3542 | **95,85%** |

O relatorio HTML e o `Summary.txt` do ReportGenerator truncam para uma casa decimal e mostram
96,7% e 95,8%; o gate usa a contagem exata de linhas, sem esse arredondamento (6284 / 6498 =
96,71% e 3395 / 3542 = 95,85%). Informativo (nao entra no gate): a cobertura de **ramos** e de
85,4% na medicao bruta (672 de 786) e de 85,9% com exclusoes (655 de 762).

## Por assembly

Cada assembly e um projeto de producao do servico. Os projetos de teste e o `Tests.Common`
ficam fora da medicao (`coverage.runsettings`). Os valores por assembly saem do relatorio HTML
gerado (`index.html` de cada pasta); a coluna de percentual e a divisao exata.

| Assembly | Bruta (cobertas / cobertaveis) | Bruta | Com exclusoes (cobertas / cobertaveis) | Com exclusoes | Linhas excluidas |
|----------|--------------------------------|-------|----------------------------------------|---------------|------------------|
| `OficinaMecanica.Domain` | 452 / 498 | 90,76% | 452 / 498 | 90,76% | 0 |
| `OficinaMecanica.Application` | 1080 / 1100 | 98,18% | 1072 / 1092 | 98,17% | 8 |
| `OficinaMecanica.Infrastructure` | 4275 / 4361 | 98,03% | 1512 / 1555 | 97,23% | 2806 |
| `OficinaMecanica.API` | 314 / 376 | 83,51% | 196 / 234 | 83,76% | 142 |
| `OficinaMecanica.Contracts` | 163 / 163 | 100% | 163 / 163 | 100% | 0 |
| **Total** | **6284 / 6498** | **96,71%** | **3395 / 3542** | **95,85%** | **2956** |

O gate vale para o **total** "com exclusoes", nao para cada assembly. O Domain (90,76%) e a API (83,76% com
exclusoes) sao os assemblies de menor cobertura, porque os controllers, as entidades e os repositorios de cadastros
(clientes, veiculos, servicos e pecas) tem trechos nao cobertos (ver
[Classes abaixo de 100%](#classes-abaixo-de-100-medicao-com-exclusoes)); o numero do gate (95,85%) e o que o CI avalia. A maior parte das linhas excluidas esta na Infrastructure
(migrations geradas pelo EF Core, `DependencyInjection*` e as configurations do EF Core).

## Testes por projeto

| Projeto de teste | Testes | O que cobre |
|------------------|-------:|-------------|
| `OficinaMecanica.Contracts.Tests` | 71 | Contratos de mensagem contra os golden JSON |
| `OficinaMecanica.Domain.Tests` | 153 | Agregados, value objects e regras de dominio (sem mocks) |
| `OficinaMecanica.Application.Tests` | 306 | Handlers e validators (Moq nas portas) |
| `OficinaMecanica.Integration.Tests` | 176 | Pipeline HTTP completo (`WebApplicationFactory`, EF InMemory, bus em memoria), middleware de excecoes e metricas HTTP |
| `OficinaMecanica.Infrastructure.Tests` (sem Docker) | 354 | Publisher e DI, health checks, maquina de estados da saga e metricas (harness do MassTransit), cliente HTTP da Execucao |
| `OficinaMecanica.Infrastructure.Tests` (`Category=Infra`, Testcontainers) | 75 | SQL Server e RabbitMQ reais em containers: outbox, inbox, saga ponta a ponta, concorrencia, sweeper, migration, prontidao, cancelamento de requisicao com comando SQL em voo |
| `OficinaMecanica.Bdd.Tests` | 12 | Cenarios BDD (Reqnroll) da saga, com a API e a saga reais e Billing/Execucao simulados |
| **Total** | **1147** | |

Em relacao a medicao anterior (1082 testes), os 65 testes a mais (Integration +57 e `Category=Infra` +8) vem do
tratamento do abandono do cliente no middleware de excecoes e das metricas HTTP por codigo de resposta.

## Exclusoes da medicao "com exclusoes"

Sao quatro padroes, e **so eles** (a lista e a mesma do `scripts/cobertura.sh`, do `ci.yml` e do
`sonar.coverage.exclusions` do SonarCloud). Todos sao arquivos gerados ou de composicao, sem regra de
negocio, e continuam sendo exercitados por outros testes:

| Padrao | O que e | Por que fica fora | Como continua verificado |
|--------|---------|-------------------|--------------------------|
| `**/Migrations/**` | Codigo gerado pelo EF Core (`dotnet ef migrations add`) | Nao e codigo escrito a mao | O `MigrationTests` (Infra) aplica a migration em um SQL Server real, a partir da `Initial`, e confere esquema, colunas, valores padrao e indices |
| `**/Program.cs` | Raiz de composicao da API: host, Swagger, middlewares, rotas e OpenTelemetry | Logica de montagem do processo, nao de negocio | Os testes de integracao (`WebApplicationFactory<Program>`), o BDD e os testes de infraestrutura sobem a API inteira |
| `**/DependencyInjection*.cs` | Registro de servicos no container de injecao de dependencia | Apenas liga as pecas | Os testes de registro no DI e de partida da configuracao (Infrastructure.Tests) e os que sobem o host |
| `**/Data/Configurations/**` | Mapeamentos do EF Core (tabelas, tipos, indices) | Declarativos: so tem efeito quando executados contra um banco | Sao aplicados sempre que o EF monta o modelo (testes com EF InMemory e com SQL Server real); os testes de infraestrutura conferem o esquema e as consultas |

A lista e curta de proposito e o numero bruto e publicado ao lado do numero com exclusoes.

### O que **nao** e excluido, de proposito

- **`CompilerGeneratedAttribute` nao esta na lista de exclusao.** Exclui-lo tiraria da medicao os iteradores, as
  expressoes lambda e as maquinas de estado de `async`, que sao codigo escrito por nos, e inflaria o numero. Esta
  explicado no `coverage.runsettings`.
- **`[ExcludeFromCodeCoverage]` e honrado pelo coverlet** (assim como `GeneratedCodeAttribute`): o codigo marcado some,
  em silencio, das **duas** medicoes e do gate. Por isso o uso e proibido sem justificativa em ADR, e o
  `scripts/cobertura.sh relatorio` lista as ocorrencias no log e no Job Summary. **Hoje ha 0 ocorrencias em `src/`.**

## Classes abaixo de 100% (medicao com exclusoes)

Ha 147 linhas nao cobertas, em 43 classes. As que mais pesam:

| Classe | Linhas nao cobertas | Cobertura de linhas |
|--------|--------------------:|---------------------|
| `OficinaMecanica.API.Controllers.ClientesController` | 12 | 42,8% |
| `OficinaMecanica.Application.Queries.ObterCliente.ObterClienteQueryHandler` | 11 | 0% |
| `OficinaMecanica.Domain.Entities.Peca` | 8 | 84% |
| `OficinaMecanica.Infrastructure.Saga.ObservadorDeMetricasDaSaga` | 7 | 83,3% |
| `OficinaMecanica.Infrastructure.Repositories.ClienteRepository` | 6 | 80% |
| `OficinaMecanica.Domain.Entities.Veiculo` / `Servico` | 6 / 6 | 79,3% / 81,2% |
| `OficinaMecanica.API.Controllers.VeiculosController` / `ServicosController` / `PecasController` | 6 / 6 / 6 | 66,6% / 71,4% / 75% |

Os pontos mais fracos estao nos cadastros (clientes, veiculos, servicos e pecas), nao na saga: o relatorio mostra
metodos dos controllers e dos repositorios desses cadastros sem cobertura.

## Como regenerar

```bash
bash scripts/cobertura.sh          # roda tudo e aplica o gate (precisa de Docker para os testes de infraestrutura)
bash scripts/ci-local.sh           # os mesmos passos do job "Build & Test" do CI, incluindo esta cobertura (~9 min)
```

O relatorio HTML sai em **`TestResults/report-com-exclusoes/index.html`**. `scripts/cobertura.sh` aceita uma fase:
`testes-sem-infra`, `testes-infra` ou `relatorio` (este so une os resultados ja gerados e aplica o gate). A saida fica
em `TestResults/`:

| Arquivo | Conteudo |
|---------|----------|
| `TestResults/report-com-exclusoes/index.html` | **Relatorio HTML**: a medicao que vale para o gate |
| `TestResults/report-bruto/index.html` | Relatorio HTML da medicao bruta |
| `TestResults/report-com-exclusoes/Summary.txt` | Resumo em texto (`Covered lines`, `Coverable lines`), que o gate le |
| `TestResults/resumo-cobertura.md` | Resumo em Markdown (vai para o Job Summary do GitHub Actions) |
| `TestResults/<projeto>/*.trx` e `TestResults/infra-<projeto>/*.trx` | Resultado de cada projeto de teste, sem e com `Category=Infra` |

## O gate de 80%

O limite vem de `COBERTURA_MINIMA` (padrao 80; o `ci.yml` o exporta como `"80"`). Funciona assim:

1. `scripts/cobertura.sh relatorio` une todos os `coverage.opencover.xml` (ReportGenerator) em duas medicoes: **bruta** e
   **com exclusoes** (os quatro padroes acima).
2. `scripts/verificar-cobertura.sh` le `Covered lines` e `Coverable lines` do `Summary.txt` **com exclusoes** e compara
   `cobertas * 100 >= minimo * cobertaveis`, sem arredondar (79,95% nao passa como 80%).
3. Abaixo do limite o script sai com codigo 1 e o job `Build & Test` reprova; entrada invalida (arquivo, minimo ou
   contagem ausente) sai com codigo 2 e tambem nao aprova.
4. O proprio gate e testado: `scripts/testar-scripts.sh` exercita o `verificar-cobertura.sh` e o `cobertura.sh relatorio`
   com entradas sinteticas (79% reprova, 81% aprova) e planta defeitos nos scripts (gate que sai 0, chamada do gate apagada,
   gate lendo o numero bruto), exigindo que cada um seja detectado.

## Onde fica no CI

No job **Build & Test** (`.github/workflows/ci.yml`), os steps "Testes (Category!=Infra)", "Testes (Category=Infra,
Testcontainers)" e "Cobertura mesclada e gate" produzem os mesmos arquivos. Depois da execucao, ficam disponiveis como
artefatos do job:

- **`relatorio-de-cobertura`**: as pastas `report-com-exclusoes` e `report-bruto`, com o `index.html` de cada uma;
- **`cobertura-opencover`**: os `coverage.opencover.xml` que o job `Quality` importa no SonarCloud (retencao de 7 dias);
- **`resultados-dos-testes`**: os `.trx`;
- o **Job Summary** do run, com a tabela de cobertura e as ocorrencias de `[ExcludeFromCodeCoverage]`.

**Ainda nao executado no GitHub**: nenhum desses artefatos existe ate a primeira execucao do pipeline.

## SonarCloud

- Badge/link do SonarCloud: **preencher apos a publicacao**
- Cobertura e Quality Gate reportados pelo SonarCloud: **preencher apos a publicacao** (o job `Quality` ainda nao rodou;
  depende da organizacao, do projeto e do `SONAR_TOKEN`)
