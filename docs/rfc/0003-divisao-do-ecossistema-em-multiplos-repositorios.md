# RFC 0003: Divisao do ecossistema em multiplos repositorios

## Status
Aceito

## Resumo
`OficinaMecanica` comecou como um unico repositorio contendo a aplicacao,
autenticacao propria (JWT simetrico, admin hardcoded) e todo o Terraform
de infraestrutura (`infra/`). Ao longo do projeto, autenticacao virou o
servico `OficinaMecanica.Seguranca`, o Terraform saiu inteiro pra
`OficinaMecanica.Infra`, e o banco de dados foi extraido ainda mais fundo
pra `OficinaMecanica.Banco` - deixando `OficinaMecanica` como um
repositorio so de aplicacao, sem `infra/` nem logica de emissao de token
proprias.

## Motivacao
Com tudo num repositorio so, alguns problemas ficavam evidentes conforme o
projeto crescia:
- **Ciclos de vida diferentes**: codigo de aplicacao muda a cada PR;
  Terraform muda raramente e exige plano cuidadoso; um banco de dados quase
  nunca deveria ser destruido/recriado. Misturar os tres no mesmo
  repositorio significa que qualquer politica de CI/CD (ex: aprovacao
  manual pra mudanca de infra, deploy automatico pra codigo) tem que
  conviver no mesmo pipeline, ou ser gambiarrada com filtros de path.
- **Responsabilidade transversal presa ao monolito**: autenticacao nao e
  uma regra de negocio de oficina mecanica - e uma preocupacao que
  qualquer servico futuro do ecossistema precisaria (ver RFC 0001 do
  `OficinaMecanica.Seguranca`). Mante-la dentro do monolito significaria
  acoplar o ciclo de vida/deploy da autenticacao ao do dominio de negocio.
- **Infra compartilhada nao pertence a nenhum app especifico**: recursos
  como o cluster AKS, o Key Vault ou a API Management sao consumidos por
  mais de um servico (`OficinaMecanica` e `OficinaMecanica.Seguranca`) -
  nao fazia sentido esses recursos "pertencerem" ao repositorio de um dos
  dois.

## Proposta
Dividir o ecossistema em 4 repositorios independentes, cada um com seu
proprio Git, CI/CD e `CLAUDE.md`:
- `OficinaMecanica` (este repositorio): so aplicacao - API .NET 8, sem
  `infra/` nem emissao de token propria, so validacao de JWT via JWKS.
- `OficinaMecanica.Seguranca`: servico de autenticacao/autorizacao
  extraido, emite os tokens que `OficinaMecanica` passa a so validar.
- `OficinaMecanica.Infra`: todo o Terraform do ecossistema (AKS, ACR, Key
  Vault, APIM, identidades OIDC, Helm releases) - ver RFC 0001 desse
  repositorio.
- `OficinaMecanica.Banco`: state e Terraform do Azure SQL Database,
  extraido ainda mais fundo do `OficinaMecanica.Infra` - ver RFC 0001
  desse repositorio tambem.

Os repositorios de aplicacao (`OficinaMecanica`,
`OficinaMecanica.Seguranca`) se conectam em runtime via HTTP (JWKS), nunca
em tempo de compilacao - nenhum dos dois referencia o codigo do outro.

## Alternativas consideradas
- **Manter tudo num monorepo, com pastas separadas por responsabilidade**:
  evita o overhead de gerenciar 4 repositorios/CI's separados, mas nao
  resolve o problema de ciclos de vida/politicas de CI diferentes -
  qualquer mudanca de infra ou de autenticacao continuaria competindo pela
  mesma esteira de PR/CI do codigo de aplicacao, e simularia pior a
  realidade de um ecossistema de microsservicos de mercado (que raramente
  compartilham repositorio entre times/servicos diferentes).
- **Extrair so a autenticacao, manter o Terraform dentro do
  `OficinaMecanica`**: reduziria o escopo dessa divisao, mas manteria o
  problema original de infra compartilhada (AKS, Key Vault) presa ao
  repositorio de um unico servico de aplicacao - qualquer mudanca de
  infra que afetasse os dois servicos exigiria decidir arbitrariamente em
  qual dos dois repositorios ela deveria morar.
- **Repositorio por camada em vez de por servico** (ex: um repo so de
  `Domain`, outro de `Infrastructure`): decomposicao errada pro problema -
  o objetivo era isolar servicos com ciclo de vida/deploy proprios
  (aplicacao, autenticacao, infraestrutura, banco), nao camadas internas
  de uma mesma aplicacao, que continuam vivendo juntas dentro de cada
  repositorio de servico.

## Decisao
Ecossistema dividido em 4 repositorios independentes por responsabilidade
(aplicacao, autenticacao, infraestrutura, banco de dados), conectados
apenas em runtime (HTTP) ou via Terraform state, nunca por dependencia de
compilacao.
