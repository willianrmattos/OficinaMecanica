# Evidencia do e2e com o codigo publicado - 2026-11-10

Quarta rodada do teste ponta a ponta local do OS Service, do Billing e da Execucao, agora sobre o **codigo que esta publicado**: as imagens
`oficina-e2e/{os,billing,execucao}:local` foram construidas das working trees limpas dos tres repositorios, cuja arvore e **identica** a de
`origin/release` e a de `origin/main` (conferido com `git diff --name-only origin/release origin/main`, vazio nos tres). As evidencias de
2026-11-08 **nao foram alteradas**: sao o registro historico de antes da publicacao.
A saida bruta esta em `e2e/evidencias/2026-11-10-execucao-publicada-saida.txt`.

## Estado testado

```text
OficinaMecanica          origin/release 08f7f86 (Merge pull request #9)   working tree limpa ao construir a imagem
OficinaMecanica.Billing  origin/release fa46678 (Merge pull request #2)   working tree limpa ao construir a imagem
OficinaMecanica.Execucao origin/release a1d28f6 (Merge pull request #2)   working tree limpa ao construir a imagem
```

Ambiente: `bash e2e/subir.sh` sobre um ambiente novo (SQL Server, MongoDB em replica set, RabbitMQ, Mailpit e JWKS de teste no lugar do
Seguranca, gateway Fake). A porta da Execucao foi trocada por `E2E_PORTA_EXECUCAO=18182`, porque a 18082 estava ocupada por um
`kubectl port-forward` esquecido na maquina; nenhuma outra porta mudou.

## Resultado

```text
01-caminho-feliz                         PASSOU   asserts=117|falhas=0|tempo=42s
02-recusa-do-orcamento                   PASSOU   asserts=43|falhas=0|tempo=13s
03-pagamento-expirado                    PASSOU   asserts=55|falhas=0|tempo=28s
04-execucao-inviavel-com-estorno         PASSOU   asserts=57|falhas=0|tempo=30s
05-resiliencia-billing-fora              PASSOU   asserts=121|falhas=0|tempo=104s
06-idempotencia                          PASSOU   asserts=71|falhas=0|tempo=37s
07-resiliencia-broker-fora               PASSOU   asserts=68|falhas=0|tempo=88s
99-verificacoes-finais                   FALHOU   asserts=54|falhas=1|tempo=23s     (primeira passada)
99-verificacoes-finais                   PASSOU   asserts=54|falhas=0|tempo=20s     (reexecucao, antes da correcao)
99-verificacoes-finais                   PASSOU   asserts=54|falhas=0|tempo=19s     (reexecucao, depois da correcao)
```

Os sete cenarios passaram na primeira passada (532 asserts, 0 falhas). **A rodada completa nao foi 8 de 8**: as verificacoes finais reprovaram
1 de 54 asserts.

## A falha das verificacoes finais

```text
[FALHA] filas de trabalho (os-saga, billing-comandos, execucao-comandos) drenadas: esperado '0', obtido '1'
  fila billing-comandos: mensagens=1 consumidores=1
```

Causa: a checagem lia a soma das mensagens das filas de trabalho **em um unico instante**, 1 segundo depois do cenario 07 (que derruba e religa o
RabbitMQ), quando uma mensagem ainda estava em transito para o consumidor. Nao era mensagem presa:

- consultada logo em seguida, a API de management mostrava as tres filas com `ready=0`, `unacked=0` e 1 consumidor cada;
- as filas `*_error` estavam vazias na mesma checagem (`soma das mensagens em filas *_error = 0`);
- os outbox do OS, do Billing e da Execucao estavam vazios (`0`) ao fim de cada cenario e nas verificacoes finais.

Correcao (em `e2e/cenarios/99-verificacoes-finais.sh`): a checagem passou a usar `esperar_valor` com teto de 30 s, o mesmo helper que as
checagens de outbox ao lado ja usam. Uma fila que nao drena em 30 s continua reprovando. Reexecutada so as verificacoes finais
(`bash e2e/executar-cenarios.sh --so 99`), o assert saiu `drenadas = '0' (apos 0 s)` e as 54 passaram.

O que **nao** foi feito: uma nova rodada completa (cenarios 01 a 07 e finais) com a checagem corrigida; so as finais foram reexecutadas sobre o
ambiente que os cenarios deixaram.

## O que esta rodada nao prova

- Nao exercita o Seguranca real, o Mercado Pago real nem o cluster (o JWKS e de teste e o gateway e o Fake). A validacao no AKS, pelo APIM e
  com o gateway real do Mercado Pago, e um roteiro a parte e nao faz parte desta evidencia.
