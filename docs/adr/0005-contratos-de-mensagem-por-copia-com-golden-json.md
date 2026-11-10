# ADR 0005: Contratos de mensagem copiados por servico, travados por golden JSON

## Status
Aceito

## Contexto
Os tres servicos trocam as mesmas mensagens (comandos e eventos da Saga). O MassTransit
identifica o tipo da mensagem pelo namespace e nome (`urn:message:<namespace>:<tipo>`), entao
produtor e consumidor precisam concordar nos dois. O ecossistema tem uma regra explicita:
repositorios de codigo nao tem dependencia de compilacao entre si.

## Decisao
Cada repositorio mantem **a sua copia** dos contratos que produz ou consome, num projeto
`OficinaMecanica.Contracts` (sem dependencias), no mesmo namespace `OficinaMecanica.Contracts.*`.
A compatibilidade e garantida por **testes de contrato**: cada mensagem tem uma amostra
deterministica e um arquivo **golden JSON** (`tests/OficinaMecanica.Contracts.Tests/Golden/`);
o teste serializa a amostra e compara com o golden, desserializa o golden de volta e
verifica que nenhum campo some. Os goldens sao **identicos** entre os repositorios e um script
de checksum, rodado na verificacao de ponta a ponta, falha se algum divergir.

Regras: evolucao so aditiva (campo novo opcional, nunca remover/renomear/mudar tipo); todo contrato
tem `MessageId`, `OrdemDeServicoId` e `OcorridoEm`; nomes de fila sao constantes em `Filas`;
nenhum dado pessoal (CPF, e-mail, telefone) nas mensagens.

Convencoes de modelagem:
- Namespaces em ingles (`Commands`, `Events`, `Common`), tipos em portugues: o namespace e parte da
  identidade da mensagem no MassTransit, entao a escolha e feita uma vez e nao muda depois.
- `MessageId` e gerado pelo produtor (Guid novo por fato de negocio, deterministico em reenvio
  idempotente) e e copiado para o `MessageId` do envelope, que e o que o inbox usa para deduplicar.
- Campos opcionais usam `Guid?`/`string?` quando o produtor pode nao ter o dado ainda (ex.:
  `CancelarOrcamento.OrcamentoId`); o consumidor localiza pelo `OrdemDeServicoId`.
- `ExecucaoFinalizada.FinalizadaEm` e o momento de negocio do reparo e pode diferir de `OcorridoEm`
  (momento de criacao da mensagem, que muda em reenvio). `MecanicoId` e string porque e o `sub`
  opaco do JWT, nao um Guid do dominio.
- `PrazoExpirado`/`TipoPrazo` existem so no catalogo do OS Service e nao sao copiados para Billing
  nem Execucao (nunca cruzam servico); por isso o enum em texto pode evoluir junto com o deploy
  desse unico servico, e o checksum de goldens compara arquivo a arquivo, so os que existem em
  mais de um repositorio.
- Dados pessoais: nenhuma mensagem leva CPF, e-mail ou telefone (teste de contrato varre as
  propriedades). `Placa` viaja porque a Execucao precisa identificar o veiculo; `ClienteId` e um id
  opaco; campos de texto livre (`Observacoes`, `Laudo`, `Motivo`) nao devem conter dado pessoal.
- Alem dos contratos, as constantes de `Filas` tem golden proprio (`Filas.json`); todo contrato com
  campo opcional tem tambem um golden de variante com opcionais nulos e listas vazias (`*.nulos.json`),
  exigido por teste.

## Consequencias
### Positivas
- Repositorios continuam independentes (sem quinto repositorio, sem feed de pacotes privado).
- Quebra de contrato aparece como teste vermelho antes de chegar a producao.

### Negativas
- A copia pode divergir se alguem esquecer de atualizar os outros repositorios; o checksum dos
  goldens mitiga, mas depende de ser executado.
- Mudar um contrato exige tres PRs coordenados.

## Alternativas consideradas
- **Pacote NuGet compartilhado (`OficinaMecanica.Contracts`)**: elimina divergencia, mas acopla
  versoes entre servicos, exige feed autenticado no CI e um quinto repositorio para publicar.
- **Schema registry/JSON Schema central**: robusto, mas infraestrutura demais para tres servicos.
