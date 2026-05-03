# Relatorio de Qualidade — OficinaMecanica

**Data:** 2026-05-05  
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
| Vulnerabilidades          | **3**      | E      | CRITICO        |
| Security Hotspots         | **2**      | —      | Atencao        |
| Code Smells               | **30**     | A      | Atencao        |
| Divida Tecnica            | **114 min**| A      | OK             |
| Cobertura de linhas       | **79,2%**  | —      | OK             |
| Cobertura de branches     | **60,6%**  | —      | Atencao        |
| Cobertura geral           | **76,3%**  | —      | OK             |
| Duplicacao de codigo      | **0,0%**   | A      | OK             |
| Testes executados         | **112**    | —      | OK             |
| Falhas nos testes         | **0**      | —      | OK             |

> Rating: **A** (Otimo) · **B** (Bom) · **C** (Regular) · **D** (Ruim) · **E** (Critico)

---

## 2. Metricas de Codigo

| Metrica                   | Valor |
|---------------------------|-------|
| Linhas de codigo (NCLOC)  | 3.151 |
| Total de linhas           | 4.117 |
| Arquivos analisados       | 134   |
| Classes                   | 144   |
| Funcoes/Metodos           | 386   |
| Complexidade ciclomatica  | 541   |
| Complexidade cognitiva    | 132   |

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
| Cobertura de linhas   | 79,2%  |
| Cobertura de branches | 60,6%  |
| Cobertura geral       | 76,3%  |
| Blocos duplicados     | 0      |
| Densidade duplicacao  | 0,0%   |

---

## 4. Issues Encontradas

### 4.1 Vulnerabilidades — 3 issues (Rating E)

Estas sao as issues mais criticas e devem ser resolvidas imediatamente.

| Severidade | Regra           | Descricao                                              | Arquivo                              |
|------------|-----------------|--------------------------------------------------------|--------------------------------------|
| BLOCKER    | `secrets:S6703` | Senha de banco hardcoded no codigo                     | `docker-compose.yml`                 |
| BLOCKER    | `secrets:S6703` | Senha de banco hardcoded no codigo                     | `src/.../appsettings.json`           |
| BLOCKER    | `csharpsquid:S6781` | Chave secreta JWT exposta no codigo               | `src/.../Services/TokenService.cs`   |

**Correcao:** Mover segredos para variaveis de ambiente ou Secret Manager. Nunca commitar senhas ou chaves em arquivos de configuracao versionados.

### 4.2 Security Hotspots — 2 issues

| Probabilidade | Descricao                                              | Arquivo              |
|---------------|--------------------------------------------------------|----------------------|
| HIGH          | Campo "password" detectado — possivel credencial hardcoded | `appsettings.json` |
| MEDIUM        | Container Docker pode estar rodando como root          | `Dockerfile`         |

**Correcao Dockerfile:** Adicionar `USER` nao-root no final do Dockerfile:
```dockerfile
RUN adduser --disabled-password --no-create-home appuser
USER appuser
```

### 4.3 Code Smells — 30 issues

#### BLOCKER (1)

| Regra               | Descricao                                              | Arquivo              |
|---------------------|--------------------------------------------------------|----------------------|
| `csharpsquid:S3875` | Sobrecarga de `operator ==` deve ser removida do ValueObject — viola o contrato de igualdade esperado | `Common/ValueObject.cs` |

#### MAJOR (21)

| Regra               | Quantidade | Descricao                                                     |
|---------------------|------------|---------------------------------------------------------------|
| `csharpsquid:S6964` | 6          | Parametros value type em actions de controller devem ser nullable (ex: `int` → `int?`) para tratamento correto de binding |
| `csharpsquid:S6966` | 3          | Usar versoes `async` dos metodos EF Core (`MigrateAsync`, `RunAsync`, `EnsureCreatedAsync`) em `Program.cs` |
| `csharpsquid:S1118` | 1          | Classe `Program` deve ter construtor `protected` ou ser declarada `static` |
| `external_roslyn:CS8618` | 8    | Propriedades nao-anulaveis sem inicializacao no construtor (construtores do EF Core) |

#### MINOR / INFO (8)

| Regra               | Descricao                                              | Arquivo              |
|---------------------|--------------------------------------------------------|----------------------|
| `csharpsquid:S1075` | URI hardcoded em `Program.cs`                          | `Program.cs`         |
| `csharpsquid:S6605` | Usar `Exists()` no lugar de `Any()` em listas         | `Cliente.cs`         |
| `csharpsquid:S6602` | Usar `.Find()` no lugar de `.FirstOrDefault()` (x2)   | `OrdemDeServico.cs`  |
| `csharpsquid:S1192` | Literal `'Troca de Oleo'` repetido 4x — extrair para constante | `OrdemDeServicoBuilder.cs` |
| `external_roslyn:CA1869` | Evitar criar nova instancia de `JsonSerializerOptions` a cada chamada | `ExceptionHandlingMiddleware.cs` |
| `external_roslyn:CA1860` | Usar `.Count > 0` ao inves de `.Any()` (x3)       | Varios              |
| `external_roslyn:CA1854` | Usar `TryGetValue` em vez de indexador de dicionario | `AppDbContext.cs`  |

### 4.4 Divida Tecnica

| Metrica         | Valor       |
|-----------------|-------------|
| Divida total    | 114 minutos |
| Ratio de divida | 0,1%        |
| Rating          | **A**       |

---

## 5. Ratings por Dimensao

| Dimensao        | Rating | Significado                        |
|-----------------|--------|------------------------------------|
| Confiabilidade  | **A**  | 0 bugs — codigo confiavel          |
| Seguranca       | **E**  | 3 vulnerabilidades BLOCKER         |
| Manutenibilidade| **A**  | Divida tecnica de apenas 0,1%      |
| Cobertura       | —      | 76,3% (proximo do recomendado 80%) |
| Duplicacao      | **A**  | 0% de codigo duplicado             |

---

## 6. Pontos Positivos da Analise

- **Zero bugs** detectados pelo Sonar — codigo robusto.
- **Zero duplicacao** — Clean Architecture bem aplicada, sem copy-paste.
- **Divida tecnica minima** (114 min) — codigo limpo e de facil manutencao.
- **90 testes passando** sem falhas — suite de testes funcional.
- **Quality Gate aprovado** — o projeto esta em estado publicavel.
- Separacao clara de responsabilidades (CQRS, DDD, Repository Pattern) reconhecida pelo scanner.

---

## 7. Plano de Acao

### Prioridade CRITICA (fazer antes do proximo deploy)

| # | Acao                                                              | Esforco |
|---|-------------------------------------------------------------------|---------|
| 1 | Remover senhas do `docker-compose.yml` e `appsettings.json`, usar variaveis de ambiente | 30 min |
| 2 | Remover/externalizar a chave JWT de `TokenService.cs` para configuracao via env var | 15 min |
| 3 | Adicionar usuario nao-root no `Dockerfile`                        | 5 min   |

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
| 8 | Aumentar cobertura de branches para ≥80% (atualmente 60,6%) com cenarios negativos | 2-4h |
| 9 | Substituir `.Any()` por `.Exists()` e `.FirstOrDefault()` por `.Find()` | 15 min |
| 10| Extrair constante para literal repetido `'Troca de Oleo'`               | 5 min   |
| 11| Singleton para `JsonSerializerOptions` no middleware de excecoes         | 10 min  |
