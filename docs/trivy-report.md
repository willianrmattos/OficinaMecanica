# Relatorio de Vulnerabilidades — OficinaMecanica

**Data:** 2026-07-08
**Ferramenta:** Trivy (aquasec/trivy:latest, v0.71.2)
**Imagem:** `oficinamecanica-api:latest`
**Imagem base:** `mcr.microsoft.com/dotnet/aspnet:8.0` (Debian 12.14 / Bookworm, runtime .NET 8.0.28)

---

## 1. Resumo Executivo

| Severidade   | Quantidade | Risco              |
|--------------|------------|--------------------|
| CRITICO      | **3**          | Correcao imediata  |
| ALTO         | **18**         | Proximo sprint     |
| MEDIO        | **62**         | Backlog priorizado |
| BAIXO        | **73**         | Monitorar          |
| DESCONHECIDO | **9**          | Avaliar            |
| **TOTAL**    | **165**        |                    |

> Em relacao ao relatorio anterior (2026-06-26, total 183), o numero caiu porque o rebuild trouxe um runtime .NET mais novo (8.0.28) e um patch level mais recente do Debian 12.14, que ja corrigiu as vulnerabilidades de `libgnutls30`/`openssl`/`libssl3` reportadas antes. Em compensacao, novos CVEs de outros pacotes (`util-linux`, `gzip`, `libacl1`) foram publicados desde entao.

---

## 2. Alvos Escaneados

| Alvo | Tipo | Vulnerabilidades |
|------|------|-----------------|
| `oficinamecanica-api:latest (debian 12.14)` | debian | 159 |
| `app/OficinaMecanica.API.deps.json` | dotnet-core | 6 |
| `usr/share/dotnet/shared/Microsoft.AspNetCore.App/8.0.28/Microsoft.AspNetCore.App.deps.json` | dotnet-core | 0 |
| `usr/share/dotnet/shared/Microsoft.NETCore.App/8.0.28/Microsoft.NETCore.App.deps.json` | dotnet-core | 0 |

---

## 3. Vulnerabilidades Criticas (CRITICAL)

Exigem correcao antes do proximo deploy em producao.

| CVE | Pacote | Versao Instalada | Versao com Fix | CVSS | Descricao |
|-----|--------|-----------------|----------------|------|-----------|
| `CVE-2026-8376` | perl-base | 5.36.0-7+deb12u3 | sem fix disponivel | 9.8 | Perl: heap buffer overflow ao compilar expressoes regulares em builds 32-bit |
| `CVE-2023-45853` | zlib1g | 1:1.2.13.dfsg-1 | sem fix disponivel | 9.8 | zlib: integer overflow e heap buffer overflow em `zipOpenNewFileInZip4_6` |
| `CVE-2026-42496` | perl-base | 5.36.0-7+deb12u3 | sem fix disponivel | 9.1 | perl-archive-tar: path traversal via symlinks manipulados permite acesso arbitrario a arquivos |

---

## 4. Vulnerabilidades Altas (HIGH)

| CVE | Pacote | Versao Instalada | Versao com Fix | CVSS | Descricao |
|-----|--------|-----------------|----------------|------|-----------|
| `CVE-2023-36414` | Azure.Identity | 1.7.0 | 1.10.2 | 8.8 | Azure Identity SDK Remote Code Execution Vulnerability |
| `CVE-2024-0056` | Microsoft.Data.SqlClient | 5.1.1 | 2.1.7 / 3.1.5 / 4.0.5 / 5.1.3 | 7.5 | Information Disclosure em MD.SqlClient (MDS) e System.Data.SqlClient (SDS) |
| `CVE-2026-53615` | bsdutils, libblkid1, libmount1, libsmartcols1, libuuid1, mount, util-linux, util-linux-extra (8 pacotes, mesmo binario fonte) | 2.38.1-5+deb12u3 | sem fix disponivel | — | Integer Overflow/Wraparound em `libblkid/src/partitions/dos.c` |
| `CVE-2025-69720` | libtinfo6, ncurses-base, ncurses-bin (3 pacotes, mesmo binario fonte) | 6.4-4 | sem fix disponivel | 7.8 | ncurses: buffer overflow pode levar a execucao de codigo arbitrario |
| `CVE-2026-9538` | perl-base | 5.36.0-7+deb12u3 | sem fix disponivel | 7.5 | perl-Archive-Tar: Denial of Service via header de tar manipulado com tamanho de entrada grande |
| `CVE-2026-48962` | perl-base | 5.36.0-7+deb12u3 | sem fix disponivel | 7.8 | perl-IO-Compress: execucao arbitraria de codigo via glob de saida controlado pelo atacante |
| `CVE-2026-42497` | perl-base | 5.36.0-7+deb12u3 | sem fix disponivel | 7.5 | perl-Archive-Tar: modificacao arbitraria de arquivos via hardlinks manipulados |
| `CVE-2026-41992` | gzip | 1.12-1 | sem fix disponivel | 7.5 | GNU gzip: buffer overflow global na descompressao LZH |
| `CVE-2026-54369` | libacl1 | 2.3.1-3 | sem fix disponivel | 7.1 | acl: escalonamento de privilegio via symlink traversal nas funcoes libacl |

---

## 5. Vulnerabilidades Medias (MEDIUM) — Top 20

| CVE | Pacote | Versao Instalada | Versao com Fix | CVSS |
|-----|--------|-----------------|----------------|------|
| `CVE-2026-13595` | bsdutils | 1:2.38.1-5+deb12u3 | - | 5.3 |
| `CVE-2026-27456` | bsdutils | 1:2.38.1-5+deb12u3 | - | 4.7 |
| `CVE-2026-3184` | bsdutils | 1:2.38.1-5+deb12u3 | - | 5.3 |
| `CVE-2025-30258` | gpgv | 2.2.40-1.1+deb12u2 | - | 4.7 |
| `CVE-2025-68972` | gpgv | 2.2.40-1.1+deb12u2 | - | 4.7 |
| `CVE-2026-41991` | gzip | 1.12-1 | - | 4.7 |
| `CVE-2026-54370` | libacl1 | 2.3.1-3 | - | 6.3 |
| `CVE-2026-54371` | libattr1 | 1:2.5.1-4 | - | 6.3 |
| `CVE-2026-13595` | libblkid1 | 2.38.1-5+deb12u3 | - | 5.3 |
| `CVE-2026-27456` | libblkid1 | 2.38.1-5+deb12u3 | - | 4.7 |
| `CVE-2026-3184` | libblkid1 | 2.38.1-5+deb12u3 | - | 5.3 |
| `CVE-2026-42250` | libbz2-1.0 | 1.0.8-5+b1 | - | 5 |
| `CVE-2026-5435` | libc-bin | 2.36-9+deb12u14 | - | 5.9 |
| `CVE-2026-5450` | libc-bin | 2.36-9+deb12u14 | - | 5 |
| `CVE-2026-5928` | libc-bin | 2.36-9+deb12u14 | - | 5 |
| `CVE-2026-6238` | libc-bin | 2.36-9+deb12u14 | - | 6.5 |
| `CVE-2026-5435` | libc6 | 2.36-9+deb12u14 | - | 5.9 |
| `CVE-2026-5450` | libc6 | 2.36-9+deb12u14 | - | 5 |
| `CVE-2026-5928` | libc6 | 2.36-9+deb12u14 | - | 5 |
| `CVE-2026-6238` | libc6 | 2.36-9+deb12u14 | - | 6.5 |
| ... | *(+42 omitidos — execute o scan completo para ver todos)* | | | |

---

## 6. Analise por Pacote

### 6.1 Pacotes com Mais Vulnerabilidades

| Pacote | Total | CRITICAL | HIGH | MEDIUM | LOW |
|--------|-------|----------|------|--------|-----|
| perl-base | 13 | 2 | 3 | 5 | 2 |
| libc-bin | 11 | 0 | 0 | 4 | 7 |
| libc6 | 11 | 0 | 0 | 4 | 7 |
| bsdutils | 7 | 0 | 1 | 3 | 2 |
| libblkid1 | 7 | 0 | 1 | 3 | 2 |
| libmount1 | 7 | 0 | 1 | 3 | 2 |
| libsmartcols1 | 7 | 0 | 1 | 3 | 2 |
| libuuid1 | 7 | 0 | 1 | 3 | 2 |
| mount | 7 | 0 | 1 | 3 | 2 |
| util-linux | 7 | 0 | 1 | 3 | 2 |
| util-linux-extra | 7 | 0 | 1 | 3 | 2 |
| libsystemd0 | 5 | 0 | 0 | 0 | 5 |

> A familia `util-linux` (bsdutils, libblkid1, libmount1, libsmartcols1, libuuid1, mount, util-linux, util-linux-extra) aparece repetida porque todos esses pacotes binarios vem do mesmo pacote-fonte Debian e compartilham os mesmos CVEs.

### 6.2 Corrigibilidade

- **Com fix disponivel:** 6 de 165 vulnerabilidades — todas em dependencias NuGet, nenhuma no SO:
  - `Azure.Identity` 1.7.0 → 1.10.2/1.11.4 (1 HIGH + 2 MEDIUM)
  - `Microsoft.Data.SqlClient` 5.1.1 → 5.1.3 (1 HIGH)
  - `Microsoft.IdentityModel.JsonWebTokens` / `System.IdentityModel.Tokens.Jwt` 7.0.3 → 7.1.2 (2 MEDIUM)
- **Sem fix disponivel:** 159 vulnerabilidades (aguardando patch do fornecedor Debian — a maior parte em `perl-base` e na familia `util-linux`)

---

## 7. Plano de Acao

### Prioridade CRITICA — antes do proximo deploy em producao

| # | Acao | CVEs Corrigidos |
|---|------|----------------|
| 1 | Atualizar `Azure.Identity` de `1.7.0` para `>= 1.11.4` via NuGet | CVE-2023-36414 (8.8 HIGH) + 2 MEDIUM |
| 2 | Atualizar `Microsoft.Data.SqlClient` de `5.1.1` para `>= 5.1.3` via NuGet | CVE-2024-0056 (7.5 HIGH) |
| 3 | Atualizar `Microsoft.IdentityModel.JsonWebTokens`/`System.IdentityModel.Tokens.Jwt` para `>= 7.1.2` via NuGet | CVE-2024-21319 (2 MEDIUM) |
| 4 | Monitorar fix para `perl-base` (3 CVEs CRITICAL/HIGH sem correcao do Debian) | Aplicar quando disponivel |
| 5 | Monitorar fix para `zlib1g` CVE-2023-45853 (9.8 CRITICAL, sem fix no Debian ha mais de um ano) | Aplicar quando disponivel |

### Prioridade ALTA — proximo sprint

| # | Acao | Impacto |
|---|------|---------|
| 6 | Avaliar remocao de `perl-base` da imagem (sem fix para 3 CVEs CRITICAL/HIGH) | Eliminacao de superficie de ataque |
| 7 | Adicionar usuario nao-root no Dockerfile (`USER appuser`) — ainda pendente, ver `docs/sonarqube-report.md` | Mitigacao de impacto em caso de RCE |
| 8 | Adicionar scan Trivy no CI/CD com `--exit-code 1` para CRITICAL/HIGH | Prevencao de regressao |

### Recorrente

| # | Acao |
|---|------|
| 9 | Executar `docker compose run --rm trivy` apos cada novo build |
| 10 | Atualizar imagem base mensalmente com `docker compose build --no-cache api` |
| 11 | Monitorar advisories da Debian Security Tracker para pacotes sem fix |

---

## 8. Como Executar o Scan

```bash
# Subir a API e rodar o scan (gera docs/trivy-report-raw.json)
docker compose build api && docker compose run --rm trivy

# Scan direto (sem Compose)
docker run --rm \
  -v //var/run/docker.sock:/var/run/docker.sock \
  -v "$(pwd)/docs:/reports" \
  aquasec/trivy:latest image \
  --format table \
  oficinamecanica-api:latest

# Scan com saida JSON completa (todas as severidades, usado para gerar este relatorio)
docker run --rm \
  -v //var/run/docker.sock:/var/run/docker.sock \
  -v "$(pwd)/docs:/reports" \
  aquasec/trivy:latest image \
  --format json \
  --output /reports/trivy-full.json \
  oficinamecanica-api:latest
```

---

> Relatorio gerado a partir de um scan real do Trivy. Os dados refletem o estado da imagem no momento do scan.
