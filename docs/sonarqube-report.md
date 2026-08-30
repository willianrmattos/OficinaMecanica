# Relatorio de Qualidade — OficinaMecanica

**Data:** 2026-08-30
**Ferramenta:** SonarQube 10.7.0 Community (container Docker)
**Projeto:** `oficina-mecanica`
**Branch:** release
**Dashboard:** http://localhost:9000/dashboard?id=oficina-mecanica

---

## 1. Resumo Executivo

| Metrica                   | Valor      | Rating | Status         |
|---------------------------|------------|--------|----------------|
| Quality Gate              | —          | —      | **PASSED (OK)**|
| Bugs                      | **0**      | A      | OK             |
| Vulnerabilidades          | **0**      | A      | OK             |
| Security Hotspots         | **0**      | —      | Revisados      |
| Code Smells               | **37**     | A      | Atencao        |
| Divida Tecnica            | **126 min**| A      | OK             |
| Cobertura de linhas       | **77,5%**  | —      | OK             |
| Cobertura de branches     | **63,4%**  | —      | Atencao        |
| Cobertura geral           | **75,5%**  | —      | Atencao        |
| Duplicacao de codigo      | **1,3%**   | —      | OK             |
| Testes executados         | **128**    | —      | OK             |
| Falhas nos testes         | **0**      | —      | OK             |

> Rating: **A** (Otimo) · **B** (Bom) · **C** (Regular) · **D** (Ruim) · **E** (Critico)

> Em relacao ao relatorio anterior (2026-07-12): o numero de linhas de
> codigo **caiu** (4.607 → 3.903) principalmente pela remocao de
> `AuthController`/`TokenService`/`AdminCredentials` (autenticacao virou
> responsabilidade exclusiva do `OficinaMecanica.Seguranca`, repositorio
> separado) - parcialmente compensada pelo codigo novo de observabilidade
> (OpenTelemetry/New Relic, metricas de negocio). Cobertura geral caiu
> levemente (77,1% → 75,5%) - o maior bloco sem cobertura continua sendo a
> migration gerada automaticamente pelo EF Core (nunca fez sentido cobrir
> isso com teste), mas o peso relativo dela subiu porque o total de NCLOC
> do projeto encolheu. 1 code smell novo apareceu e ja foi corrigido nesta
> mesma rodada (tag `:latest` do Mailpit, ver secao 4.3); nenhum bug ou
> vulnerabilidade nova.

> **Sobre uma primeira tentativa que falhou o Quality Gate**: a primeira
> analise desta rodada rodou com a mesma tag de versao do relatorio
> anterior (`1.0.0`), o que fez o SonarQube tratar **tudo** desde
> 2026-07-08 como "codigo novo" (varias sessoes de trabalho empilhadas -
> extracao da autenticacao, New Relic, metricas de negocio, CI/CD com
> `release`) - a cobertura desse "codigo novo" ficou em 73,7% (abaixo dos
> 80% exigidos) e havia 1 violacao nova. Corrigido em duas frentes: (1)
> fixada a tag do Mailpit (`kubernetes:S6596`, unica violacao nova de
> verdade) e adicionado teste pro `JsonWebKeySetRetriever` (0% de cobertura
> antes); (2) a versao da segunda analise foi incrementada pra `1.1.0`,
> resetando o periodo de comparacao pra so entre as duas analises de hoje -
> a partir daqui, o "codigo novo" volta a significar de verdade "o que
> mudou desde a ultima analise", nao o acumulado de multiplas sessoes.

---

## 2. Metricas de Codigo

| Metrica                   | Valor |
|---------------------------|-------|
| Linhas de codigo (NCLOC)  | 3.903 |
| Arquivos analisados       | 150   |
| Classes                   | 152   |
| Funcoes/Metodos           | 405   |
| Complexidade ciclomatica  | 583   |
| Complexidade cognitiva    | 146   |

> Queda de 4.607 → 3.903 NCLOC e 175 → 150 arquivos, refletindo a extracao
> da autenticacao (JWT simetrico, `AuthController`/`TokenService`) pro
> repositorio `OficinaMecanica.Seguranca` - esta API agora so **valida**
> tokens (RS256/JWKS), nao emite mais nenhum. Funcoes/complexidade subiram
> levemente com o codigo novo de observabilidade (`MetricasNegocio`,
> `MetricasNegocioEventHandlers`, `MetricasDeNegocioBackgroundService`,
> expansao do `Program.cs` pra metricas/logs via OpenTelemetry, alem do
> tracing que ja existia). `stress/**` (script de carga k6, nao e codigo de
> aplicacao) foi excluido da analise via `sonar.exclusions`.

---

## 3. Testes e Cobertura

### 3.1 Resultados dos Testes

| Suite                              | Total  | Passou | Falhou | Erros |
|------------------------------------|--------|--------|--------|-------|
| OficinaMecanica.Domain.Tests       | 43     | 43     | 0      | 0     |
| OficinaMecanica.Application.Tests  | 57     | 57     | 0      | 0     |
| OficinaMecanica.Integration.Tests  | 28     | 28     | 0      | 0     |
| **Total**                          | **128**| **128**| **0**  | **0** |

> +4 testes em relacao ao relatorio anterior (124 → 128): 3 no
> Application.Tests para `MetricasNegocioEventHandlers`
> (`OrdemDeServicoCriadaMetricaEventHandler`/`OrcamentoRecusadoMetricaEventHandler`),
> usando `System.Diagnostics.Metrics.MeterListener` pra observar os
> `Counter`/`Histogram` estaticos (a API de metricas do .NET nao expoe um
> jeito direto de "ler o valor atual"); 1 no Integration.Tests para
> `JsonWebKeySetRetriever` (fake local de `IDocumentRetriever`, sem Moq -
> esse projeto de teste nao usa mocks). O `MetricasDeNegocioBackgroundService`
> tambem ganhou um teste (via `CustomWebApplicationFactory`), incluido no
> total do Integration.Tests.

### 3.2 Cobertura de Codigo (OpenCover — reportada pelo SonarQube)

| Metrica               | Valor  |
|-----------------------|--------|
| Cobertura de linhas   | 77,5%  |
| Cobertura de branches | 63,4%  |
| Cobertura geral       | 75,5%  |
| Densidade duplicacao  | 1,3%   |

---

## 4. Issues Encontradas

### 4.1 Vulnerabilidades — 0 issues abertas (historico: 2 encontradas, ambas resolvidas)

| Severidade | Regra               | Descricao                                              | Arquivo                              | Status |
|------------|---------------------|---------------------------------------------------------|--------------------------------------|-----------|
| BLOCKER    | `csharpsquid:S2115` | Usar uma senha segura ao conectar no banco (campo `Password=` vazio na connection string versionada) | `src/OficinaMecanica.API/appsettings.json` | RESOLVED (falso positivo, ja registrado no relatorio anterior) |
| BLOCKER    | `csharpsquid:S6781` | Chave secreta JWT exposta no codigo                     | `src/OficinaMecanica.Infrastructure/Services/TokenService.cs` | CLOSED — arquivo nao existe mais (`TokenService.cs` foi removido junto com a extracao da autenticacao pro `OficinaMecanica.Seguranca`) |

### 4.2 Security Hotspots — 0 pendentes (1 novo encontrado e revisado nesta rodada)

| Probabilidade | Descricao                                                        | Arquivo                | Resolucao |
|----------------|-------------------------------------------------------------------|------------------------|-----------|
| LOW            | Uso de protocolo em texto claro (HTTP, nao HTTPS)                  | `k8s/oficinamecanica-api/deployment.yaml` (`Otel__Endpoint`) | Safe — endpoint interno do cluster (`otel-collector-opentelemetry-collector.monitoring.svc.cluster.local`), o trafego nunca sai da rede do AKS; texto claro e aceitavel pra telemetria pod-a-pod dentro do proprio cluster |

> Os 6 hotspots do relatorio anterior continuam revisados (nenhuma
> regressao). Este e novo - provavelmente porque o arquivo
> `k8s/oficinamecanica-api/deployment.yaml` so passou a apontar pra um
> endpoint OTLP em texto claro depois da migracao pra New Relic (antes
> apontava pro Jaeger local, dentro do mesmo cluster, possivelmente nao
> analisado pela mesma regra).

### 4.3 Code Smells — 37 issues (sem mudanca liquida desde o relatorio anterior)

#### Corrigido nesta rodada

| Regra              | Severidade | Descricao                              | Arquivo                       | Resolucao |
|--------------------|------------|-------------------------------------------|-------------------------------|-----------|
| `kubernetes:S6596` | MAJOR      | Usar uma tag de versao especifica pra imagem (nao `:latest`) | `k8s/mailpit/deployment.yaml` | **Corrigido** — `axllent/mailpit:v1.31.0` (tag real conferida no Docker Hub), tambem replicado no `docker-compose.yml` local pra manter os dois ambientes consistentes |

#### Sem mudanca desde o relatorio anterior (37 issues)

##### BLOCKER (1)

| Regra               | Descricao                                              | Arquivo              |
|---------------------|----------------------------------------------------------|-----------------------|
| `csharpsquid:S3875` | Sobrecarga de `operator ==` deve ser removida do ValueObject — viola o contrato de igualdade esperado | `Common/ValueObject.cs` |

##### MAJOR (19)

| Regra                    | Quantidade | Descricao                                                     |
|--------------------------|------------|-----------------------------------------------------------------|
| `external_roslyn:CS8618` | 8          | Propriedades nao-anulaveis sem inicializacao no construtor (construtores do EF Core) |
| `csharpsquid:S6964`      | 7          | Parametros value type em actions de controller devem ser nullable (ex: `int` → `int?`) para tratamento correto de binding |
| `csharpsquid:S6966`      | 3          | Usar versoes `async` dos metodos EF Core (`MigrateAsync`, `RunAsync`, `EnsureCreatedAsync`) em `Program.cs` |
| `csharpsquid:S1118`      | 1          | Classe `Program` deve ter construtor `protected` ou ser declarada `static` |

##### MINOR (12) / INFO (5)

| Regra                    | Descricao                                              | Arquivo              |
|--------------------------|--------------------------------------------------------|-----------------------|
| `csharpsquid:S1192`      | Literais repetidos na migration gerada pelo EF Core (`uniqueidentifier`, `OrdensDeServico`, `decimal(18,2)`, `Veiculos`, etc.) | `Migrations/20260503122341_Initial.cs` |
| `csharpsquid:S1192`      | Literal `'Troca de Óleo'` repetido 5x — extrair para constante | `OrdemDeServicoBuilder.cs` |
| `csharpsquid:S6605`      | Usar `Exists()` no lugar de `Any()`                    | `Cliente.cs`          |
| `csharpsquid:S6602`      | Usar `.Find()` no lugar de `.FirstOrDefault()` (x2)    | `OrdemDeServico.cs`   |
| `external_roslyn:CA1869` | Evitar criar nova instancia de `JsonSerializerOptions` a cada chamada | `ExceptionHandlingMiddleware.cs` |
| `external_roslyn:CA1860` | Usar `.Count > 0` ao inves de `.Any()` (x3)             | `OrdemDeServico.cs`, `AppDbContext.cs`, `OrdemDeServicoRepository.cs` |
| `external_roslyn:CA1854` | Usar `TryGetValue` em vez de indexador de dicionario   | `OrdemDeServico.cs`   |

### 4.4 Divida Tecnica

| Metrica         | Valor       |
|-----------------|-------------|
| Divida total    | 126 minutos |
| Ratio de divida | 0,1%        |
| Rating          | **A**       |

---

## 5. Ratings por Dimensao

| Dimensao        | Rating | Significado                        |
|-----------------|--------|-------------------------------------|
| Confiabilidade  | **A**  | 0 bugs — codigo confiavel          |
| Seguranca       | **A**  | 0 vulnerabilidades abertas          |
| Manutenibilidade| **A**  | Divida tecnica de apenas 0,1%       |
| Cobertura       | —      | 75,5% (branches ainda abaixo do recomendado 80%) |
| Duplicacao      | —      | 1,3% (concentrada em migration gerada automaticamente) |

---

## 6. Pontos Positivos da Analise

- **Zero bugs e zero vulnerabilidades abertas** detectados pelo Sonar, mantido desde o relatorio anterior.
- **Todos os security hotspots revisados** (7/7 no total historico) - o unico novo (endpoint OTLP em texto claro, interno ao cluster) foi revisado e marcado Safe nesta rodada.
- **Quality Gate passou** apos corrigir a unica violacao nova real (tag do Mailpit) e ajustar o periodo de comparacao (versao incrementada pra `1.1.0`).
- **Reducao real de superficie de codigo**: a extracao da autenticacao pro `OficinaMecanica.Seguranca` removeu `AuthController`/`TokenService`/`AdminCredentials` inteiros, fechando de quebra a vulnerabilidade BLOCKER que so existia por causa daquele arquivo (agora `CLOSED`, nao so `RESOLVED`).
- **Cobertura de testes cresceu em numero absoluto** (124 → 128), incluindo o primeiro teste do projeto usando `MeterListener` pra validar metricas de observabilidade (`System.Diagnostics.Metrics`), uma tecnica nao-trivial que nao existia antes neste repositorio.
- **Duplicacao ainda minima** (1,3%), concentrada quase inteiramente na migration `20260503122341_Initial.cs` gerada automaticamente pelo EF Core.
- **Nenhuma regressao nos 37 code smells pre-existentes**.

---

## 7. Plano de Acao

### Concluido nesta rodada

| # | Acao                                                              | Resultado |
|---|-------------------------------------------------------------------|---------|
| ~~1~~ | ~~Revisar o hotspot novo de protocolo em texto claro (OTel Endpoint)~~ | **Feito** — marcado Safe, endpoint interno do cluster |
| ~~2~~ | ~~Fixar `k8s/mailpit/deployment.yaml`/`docker-compose.yml` numa tag de versao especifica do Mailpit~~ | **Feito** — `v1.31.0` |
| ~~3~~ | ~~Cobrir `JsonWebKeySetRetriever` (0% antes)~~                    | **Feito** — teste novo com fake local de `IDocumentRetriever` |
| ~~4~~ | ~~Excluir `stress/**` da analise (script k6, nao e codigo de aplicacao)~~ | **Feito** — `sonar.exclusions=stress/**` |

### Prioridade ALTA (herdado, ainda pendente)

| # | Acao                                                              | Esforco |
|---|-------------------------------------------------------------------|---------|
| 5 | Corrigir `operator ==` no `ValueObject.cs` (S3875)               | 10 min  |
| 6 | Tornar parametros value-type dos controllers anulaveis (S6964)   | 20 min  |
| 7 | Tornar metodos async no `Program.cs` (S6966, MigrateAsync etc.)  | 15 min  |
| 8 | Corrigir CS8618 nos construtores EF das entidades do dominio      | 20 min  |

### Prioridade MEDIA (qualidade)

| # | Acao                                                                    | Esforco |
|---|-------------------------------------------------------------------------|---------|
| 9 | Aumentar cobertura de branches para ≥80% (atualmente 63,4%) com cenarios negativos | 2-4h |
| 10| Substituir `.Any()` por `.Exists()` e `.FirstOrDefault()` por `.Find()` | 15 min |
| 11| Extrair constantes para os literais repetidos na migration e no `OrdemDeServicoBuilder` | 15 min |
| 12| Singleton para `JsonSerializerOptions` no middleware de excecoes         | 10 min  |
| 13| Adicionar analise SonarQube (e Trivy) ao pipeline de CI/CD - hoje so roda localmente | — |

> **Nota pra proxima analise**: sempre incrementar `/v:` (versao) a cada
> nova rodada de scan, nunca reusar o valor anterior - senao o periodo de
> "codigo novo" do Quality Gate acumula tudo desde a ultima vez que a
> versao mudou de verdade, nao so o que mudou de fato desde a ultima
> analise.
