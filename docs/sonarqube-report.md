# Relatorio de Qualidade — OficinaMecanica

**Data:** 2026-07-12
**Ferramenta:** SonarQube 10.7.0 Community (container Docker)
**Projeto:** `oficina-mecanica`
**Branch:** main
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
| Cobertura de linhas       | **79,0%**  | —      | OK             |
| Cobertura de branches     | **63,7%**  | —      | Atencao        |
| Cobertura geral           | **77,1%**  | —      | Atencao        |
| Duplicacao de codigo      | **1,1%**   | —      | OK             |
| Testes executados         | **124**    | —      | OK             |
| Falhas nos testes         | **0**      | —      | OK             |

> Rating: **A** (Otimo) · **B** (Bom) · **C** (Regular) · **D** (Ruim) · **E** (Critico)

> Em relacao ao relatorio anterior (2026-07-08): as 2 vulnerabilidades BLOCKER foram investigadas e marcadas como **falso positivo** no SonarQube com justificativa registrada (ver secao 4.1); os 5 security hotspots foram revisados (4 marcados **Safe** por serem decisoes de design ja documentadas ou falso positivo de regra desatualizada, 1 **corrigido de fato** no Dockerfile) e mais 1 hotspot pre-existente (Key Vault) foi identificado e revisado nesta rodada; a cobertura subiu de 70,6% para 77,1% com os testes novos da fase 2 (recusa de orcamento, listagem, canal de e-mail).

---

## 2. Metricas de Codigo

| Metrica                   | Valor |
|---------------------------|-------|
| Linhas de codigo (NCLOC)  | 4.607 |
| Arquivos analisados       | 175   |
| Classes                   | 152   |
| Funcoes/Metodos           | 403   |
| Complexidade ciclomatica  | 575   |
| Complexidade cognitiva    | 137   |

> O aumento em relacao ao relatorio anterior (3.877 NCLOC / 155 arquivos) reflete as 3 funcionalidades novas da fase 2: recusa de orcamento, listagem ordenada/filtrada e notificacao por e-mail (`SmtpEmailService`, `StatusOrdemAlteradoEventHandler`, `IEmailService`). O script `stress/ordens-de-servico-stress.js` (k6) foi excluido da analise (`sonar.exclusions=stress/**`) por nao ser codigo de aplicacao.

---

## 3. Testes e Cobertura

### 3.1 Resultados dos Testes

| Suite                              | Total  | Passou | Falhou | Erros |
|------------------------------------|--------|--------|--------|-------|
| OficinaMecanica.Domain.Tests       | 43     | 43     | 0      | 0     |
| OficinaMecanica.Application.Tests  | 54     | 54     | 0      | 0     |
| OficinaMecanica.Integration.Tests  | 27     | 27     | 0      | 0     |
| **Total**                          | **124**| **124**| **0**  | **0** |

> +12 testes em relacao ao relatorio anterior (112 → 124): 2 no Domain (transicao de recusa de orcamento), 5 no Application (handler de recusa + handler do evento de e-mail) e 5 no Integration (recusa via API, listagem ordenada/filtrada, `SmtpEmailService`).

### 3.2 Cobertura de Codigo (OpenCover — reportada pelo SonarQube)

| Metrica               | Valor  |
|-----------------------|--------|
| Cobertura de linhas   | 79,0%  |
| Cobertura de branches | 63,7%  |
| Cobertura geral       | 77,1%  |
| Blocos duplicados     | 4      |
| Densidade duplicacao  | 1,1%   |

---

## 4. Issues Encontradas

### 4.1 Vulnerabilidades — 0 issues abertas (2 encontradas e resolvidas como falso positivo)

| Severidade | Regra               | Descricao                                              | Arquivo                              | Resolucao |
|------------|---------------------|---------------------------------------------------------|--------------------------------------|-----------|
| BLOCKER    | `csharpsquid:S2115` | Usar uma senha segura ao conectar no banco (campo `Password=` vazio na connection string versionada) | `src/OficinaMecanica.API/appsettings.json` | Falso positivo — o valor vazio e proposital, a senha real vem de `.env`/Secret em runtime, nunca commitada |
| BLOCKER    | `csharpsquid:S6781` | Chave secreta JWT exposta no codigo                     | `src/OficinaMecanica.Infrastructure/Services/TokenService.cs` | Falso positivo — a chave e lida via `IConfiguration` (`JwtSettings:SecretKey`), nao ha nenhum literal hardcoded no arquivo |

Ambos os achados foram revisados e marcados no SonarQube (`do_transition` → `falsepositive`) com a justificativa acima registrada como comentario na issue.

### 4.2 Security Hotspots — 0 pendentes (6 revisados)

| Probabilidade | Descricao                                                        | Arquivo                | Resolucao |
|----------------|-------------------------------------------------------------------|------------------------|-----------|
| MEDIUM         | Container Docker pode estar rodando como root                     | `Dockerfile`           | **Corrigido** — adicionado `USER $APP_UID` (usuario nao-root ja disponivel na imagem base) |
| MEDIUM         | Acesso publico a rede habilitado                                   | `infra/sqldb/main.tf`    | Safe — decisao de design documentada (Azure SQL Database e publico por natureza, controle via firewall rules) |
| MEDIUM         | Omitir `enable_rbac_authorization` desabilita RBAC                | `infra/keyvault/main.tf` | Safe — falso positivo de regra desatualizada. RBAC ja esta habilitado via `rbac_authorization_enabled` (nome do atributo renomeado no provider `azurerm` v4); a regra do Sonar ainda procura o nome antigo |
| LOW            | Ausencia de bloco `identity` desabilita Azure Managed Identities  | `infra/acr/main.tf`      | Safe — sem caso de uso hoje (nenhuma feature depende de managed identity nesse recurso) |
| LOW            | Ausencia de bloco `identity` desabilita Azure Managed Identities  | `infra/sqldb/main.tf`    | Safe — mesmo motivo acima |
| LOW            | Ausencia de bloco `identity` desabilita Azure Managed Identities  | `infra/storage/main.tf`  | Safe — mesmo motivo acima |

> O hotspot do Key Vault nao aparecia no relatorio anterior (5 hotspots) — foi identificado nesta rodada de revisao. Os demais 5 ja eram conhecidos.

### 4.3 Code Smells — 37 issues (sem mudanca em relacao ao relatorio anterior)

Nenhum code smell novo foi introduzido pelo codigo da fase 2 — os 37 abaixo sao os mesmos do relatorio de 2026-07-08.

#### BLOCKER (1)

| Regra               | Descricao                                              | Arquivo              |
|---------------------|--------------------------------------------------------|-----------------------|
| `csharpsquid:S3875` | Sobrecarga de `operator ==` deve ser removida do ValueObject — viola o contrato de igualdade esperado | `Common/ValueObject.cs` |

#### MAJOR (19)

| Regra                    | Quantidade | Descricao                                                     |
|--------------------------|------------|-----------------------------------------------------------------|
| `external_roslyn:CS8618` | 8          | Propriedades nao-anulaveis sem inicializacao no construtor (construtores do EF Core) |
| `csharpsquid:S6964`      | 7          | Parametros value type em actions de controller devem ser nullable (ex: `int` → `int?`) para tratamento correto de binding |
| `csharpsquid:S6966`      | 3          | Usar versoes `async` dos metodos EF Core (`MigrateAsync`, `RunAsync`, `EnsureCreatedAsync`) em `Program.cs` |
| `csharpsquid:S1118`      | 1          | Classe `Program` deve ter construtor `protected` ou ser declarada `static` |

#### MINOR (12) / INFO (5)

| Regra                    | Descricao                                              | Arquivo              |
|--------------------------|--------------------------------------------------------|-----------------------|
| `csharpsquid:S1192`      | Literais repetidos na migration gerada pelo EF Core (`uniqueidentifier` x16, `OrdensDeServico` x6, `nvarchar(200)` x6, `decimal(18,2)` x5, `Clientes`/`datetime2`/`bigint`/`Veiculos` x4 cada) | `Migrations/20260503122341_Initial.cs` |
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
| Seguranca       | **A**  | 0 vulnerabilidades abertas — as 2 encontradas foram falso positivo, com justificativa registrada |
| Manutenibilidade| **A**  | Divida tecnica de apenas 0,1%       |
| Cobertura       | —      | 77,1% (branches ainda abaixo do recomendado 80%) |
| Duplicacao      | —      | 1,1% (concentrada em migration gerada automaticamente) |

---

## 6. Pontos Positivos da Analise

- **Zero bugs e zero vulnerabilidades abertas** detectados pelo Sonar.
- **Todos os security hotspots revisados** (6/6) — 1 corrigido de fato (usuario nao-root no Dockerfile), 5 confirmados como decisao de design segura ou falso positivo, cada um com justificativa registrada.
- **Duplicacao minima (1,1%)**, concentrada quase inteiramente na migration `20260503122341_Initial.cs` gerada automaticamente pelo EF Core — nao e codigo escrito a mao.
- **Divida tecnica minima** (126 min, ratio 0,1%) — codigo limpo e de facil manutencao.
- **124 testes passando** sem falhas — suite de testes cresceu 12 casos nesta fase sem introduzir nenhum code smell novo.
- **Quality Gate aprovado**, incluindo os criterios de "new code" (cobertura de codigo novo 83,5%, 0 hotspots pendentes, 0 violacoes novas).
- Separacao clara de responsabilidades (CQRS, DDD, Repository Pattern, Event Handlers) reconhecida pelo scanner.

---

## 7. Plano de Acao

### Concluido nesta rodada

| # | Acao                                                              | Resultado |
|---|-------------------------------------------------------------------|---------|
| ~~1~~ | ~~Externalizar a chave JWT de `TokenService.cs`~~                 | Nao era necessario — falso positivo, a chave ja vinha de `IConfiguration` |
| ~~2~~ | ~~Adicionar usuario nao-root no `Dockerfile`~~                    | **Feito** — `USER $APP_UID` |
| ~~3~~ | ~~Revisar o hotspot do campo `Password=` vazio~~                  | Revisado e marcado Safe — comportamento intencional |

### Prioridade ALTA

| # | Acao                                                              | Esforco |
|---|-------------------------------------------------------------------|---------|
| 4 | Corrigir `operator ==` no `ValueObject.cs` (S3875)               | 10 min  |
| 5 | Tornar parametros value-type dos controllers anulaveis (S6964)   | 20 min  |
| 6 | Tornar metodos async no `Program.cs` (S6966, MigrateAsync etc.)  | 15 min  |
| 7 | Corrigir CS8618 nos construtores EF das entidades do dominio      | 20 min  |

### Prioridade MEDIA (qualidade)

| # | Acao                                                                    | Esforco |
|---|-------------------------------------------------------------------------|---------|
| 8 | Aumentar cobertura de branches para ≥80% (atualmente 63,7%) com cenarios negativos | 2-4h |
| 9 | Substituir `.Any()` por `.Exists()` e `.FirstOrDefault()` por `.Find()` | 15 min |
| 10| Extrair constantes para os literais repetidos na migration e no `OrdemDeServicoBuilder` | 15 min |
| 11| Singleton para `JsonSerializerOptions` no middleware de excecoes         | 10 min  |
| 12| Adicionar scan Trivy + analise SonarQube ao pipeline de CI/CD (ainda nao existe automacao) | — |
