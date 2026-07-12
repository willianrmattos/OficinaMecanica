# Relatorio de Qualidade — OficinaMecanica

**Data:** 2026-07-08
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
| Vulnerabilidades          | **2**      | E      | CRITICO        |
| Security Hotspots         | **5**      | —      | Atencao        |
| Code Smells               | **37**     | A      | Atencao        |
| Divida Tecnica            | **126 min**| A      | OK             |
| Cobertura de linhas       | **72,1%**  | —      | Atencao        |
| Cobertura de branches     | **60,9%**  | —      | Atencao        |
| Cobertura geral           | **70,6%**  | —      | Atencao        |
| Duplicacao de codigo      | **1,3%**   | —      | OK             |
| Testes executados         | **112**    | —      | OK             |
| Falhas nos testes         | **0**      | —      | OK             |

> Rating: **A** (Otimo) · **B** (Bom) · **C** (Regular) · **D** (Ruim) · **E** (Critico)

---

## 2. Metricas de Codigo

| Metrica                   | Valor |
|---------------------------|-------|
| Linhas de codigo (NCLOC)  | 3.877 |
| Total de linhas           | 4.886 |
| Arquivos analisados       | 155   |
| Classes                   | 145   |
| Funcoes/Metodos           | 388   |
| Complexidade ciclomatica  | 541   |
| Complexidade cognitiva    | 132   |

> O aumento de arquivos/linhas em relacao ao relatorio anterior (2026-05-05) reflete a chegada dos modulos Terraform em `infra/` (rg, storage, acr, aks, sqldb), que agora tambem sao analisados pelo SonarQube.

---

## 3. Testes e Cobertura

### 3.1 Resultados dos Testes

| Suite                              | Total  | Passou | Falhou | Erros |
|------------------------------------|--------|--------|--------|-------|
| OficinaMecanica.Domain.Tests       | 41     | 41     | 0      | 0     |
| OficinaMecanica.Application.Tests  | 49     | 49     | 0      | 0     |
| OficinaMecanica.Integration.Tests  | 22     | 22     | 0      | 0     |
| **Total**                          | **112**| **112**| **0**  | **0** |

### 3.2 Cobertura de Codigo (OpenCover — reportada pelo SonarQube)

| Metrica               | Valor  |
|-----------------------|--------|
| Cobertura de linhas   | 72,1%  |
| Cobertura de branches | 60,9%  |
| Cobertura geral       | 70,6%  |
| Blocos duplicados     | 4      |
| Densidade duplicacao  | 1,3%   |

---

## 4. Issues Encontradas

### 4.1 Vulnerabilidades — 2 issues (Rating E)

Estas sao as issues mais criticas e devem ser resolvidas imediatamente.

| Severidade | Regra               | Descricao                                              | Arquivo                              |
|------------|---------------------|---------------------------------------------------------|--------------------------------------|
| BLOCKER    | `csharpsquid:S2115` | Usar uma senha segura ao conectar no banco (campo `Password=` vazio na connection string versionada) | `src/OficinaMecanica.API/appsettings.json` |
| BLOCKER    | `csharpsquid:S6781` | Chave secreta JWT exposta no codigo                     | `src/OficinaMecanica.Infrastructure/Services/TokenService.cs` |

**Correcao:** Externalizar a chave JWT para variavel de ambiente/Secret Manager. O achado de senha hardcoded do banco em `docker-compose.yml` — presente no relatorio anterior — **ja foi resolvido**: a senha do Azure SQL agora vem de `.env` (fora do Git) via `${SQL_CONNECTION_STRING}`.

### 4.2 Security Hotspots — 5 issues

| Probabilidade | Descricao                                                        | Arquivo                |
|----------------|-------------------------------------------------------------------|------------------------|
| MEDIUM         | Container Docker pode estar rodando como root                     | `Dockerfile`           |
| MEDIUM         | Acesso publico a rede habilitado — confirmar se e intencional     | `infra/sqldb/main.tf`    |
| LOW            | Ausencia de bloco `identity` desabilita Azure Managed Identities  | `infra/acr/main.tf`      |
| LOW            | Ausencia de bloco `identity` desabilita Azure Managed Identities  | `infra/sqldb/main.tf`    |
| LOW            | Ausencia de bloco `identity` desabilita Azure Managed Identities  | `infra/storage/main.tf`  |

> Os 4 hotspots em `infra/` sao novos nesta analise — o SonarQube passou a escanear os modulos Terraform junto com o codigo .NET. O acesso publico do SQL Database e intencional (firewall restrito ao IP do cliente + regra "Allow Azure services", ver `README.md`/`CLAUDE.md`), mas os 3 hotspots de Managed Identity ausente valem uma avaliacao futura.

**Correcao Dockerfile:** Adicionar `USER` nao-root no final do Dockerfile:
```dockerfile
RUN adduser --disabled-password --no-create-home appuser
USER appuser
```

### 4.3 Code Smells — 37 issues

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
| `csharpsquid:S1192`      | Literal `'Troca de Óleo'` repetido 4x — extrair para constante | `OrdemDeServicoBuilder.cs` |
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
| Seguranca       | **E**  | 2 vulnerabilidades BLOCKER          |
| Manutenibilidade| **A**  | Divida tecnica de apenas 0,1%       |
| Cobertura       | —      | 70,6% (abaixo do recomendado 80%)  |
| Duplicacao      | —      | 1,3% (concentrada em migration gerada automaticamente) |

---

## 6. Pontos Positivos da Analise

- **Zero bugs** detectados pelo Sonar — codigo robusto.
- **Duplicacao minima (1,3%)**, concentrada inteiramente na migration `20260503122341_Initial.cs` gerada automaticamente pelo EF Core — nao e codigo escrito a mao.
- **Divida tecnica minima** (126 min, ratio 0,1%) — codigo limpo e de facil manutencao.
- **112 testes passando** sem falhas — suite de testes funcional.
- **Quality Gate aprovado** — o projeto esta em estado publicavel.
- **Um dos BLOCKERs do relatorio anterior ja foi corrigido**: a senha do banco saiu do `docker-compose.yml` e passou a vir de `.env` (fora do Git).
- Separacao clara de responsabilidades (CQRS, DDD, Repository Pattern) reconhecida pelo scanner.

---

## 7. Plano de Acao

### Prioridade CRITICA (fazer antes do proximo deploy)

| # | Acao                                                              | Esforco |
|---|-------------------------------------------------------------------|---------|
| 1 | Externalizar a chave JWT de `TokenService.cs` para variavel de ambiente/Secret Manager | 15 min |
| 2 | Adicionar usuario nao-root no `Dockerfile`                        | 5 min   |
| 3 | Revisar o hotspot do campo `Password=` vazio em `appsettings.json` (ex: mover para User Secrets local em vez de manter o campo no arquivo versionado) | 15 min |

### Prioridade ALTA

| # | Acao                                                              | Esforco |
|---|-------------------------------------------------------------------|---------|
| 4 | Corrigir `operator ==` no `ValueObject.cs` (S3875)               | 10 min  |
| 5 | Tornar parametros value-type dos controllers anulaveis (S6964)   | 20 min  |
| 6 | Tornar metodos async no `Program.cs` (S6966, MigrateAsync etc.)  | 15 min  |
| 7 | Corrigir CS8618 nos construtores EF das entidades do dominio      | 20 min  |
| 8 | Avaliar necessidade de bloco `identity` (Managed Identity) nos modulos Terraform `acr`, `sqldb`, `storage` | 30 min |

### Prioridade MEDIA (qualidade)

| # | Acao                                                                    | Esforco |
|---|-------------------------------------------------------------------------|---------|
| 9 | Aumentar cobertura de branches para ≥80% (atualmente 60,9%) com cenarios negativos | 2-4h |
| 10| Substituir `.Any()` por `.Exists()` e `.FirstOrDefault()` por `.Find()` | 15 min |
| 11| Extrair constantes para os literais repetidos na migration e no `OrdemDeServicoBuilder` | 15 min |
| 12| Singleton para `JsonSerializerOptions` no middleware de excecoes         | 10 min  |
