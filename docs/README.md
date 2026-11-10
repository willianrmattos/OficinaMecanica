# Documentacao do OS Service

Indice da pasta `docs/`. Cada tipo de documento fica na sua pasta; o resumo do repositorio esta no [README.md](../README.md) da raiz.

| Item | O que tem |
|------|-----------|
| [`adr/`](adr/) | ADRs (Architecture Decision Records): decisoes de arquitetura ja tomadas, com contexto e consequencias (0001 a 0009). |
| [`rfc/`](rfc/) | RFCs (Request for Comments): propostas e a justificativa de cada direcao tecnica, como a decomposicao em microsservicos com Saga (0001 a 0005). |
| [`c4/`](c4/) | Diagramas C4 em PlantUML: nivel 1 (contexto), nivel 2 (containers) e nivel 3 (componentes). |
| [`diagramas/`](diagramas/) | Maquina de estados da Saga (`saga-maquina-de-estados.puml`, a especificacao que um teste compara com o codigo), camadas da Clean Architecture, Event Storming (PDF) e o diagrama de sequencia (JPG, anterior a Saga). |
| [`relatorios/`](relatorios/) | Evidencia de cobertura de testes (`cobertura.md`) e os relatorios historicos do SonarQube e do Trivy; a saida bruta do `docker compose run --rm trivy` tambem cai aqui (ignorada pelo Git). |
| [`openapi.json`](openapi.json) | Contrato OpenAPI da API, gerado pela API compilada com `bash scripts/exportar-swagger.sh` e conferido no CI (`--verificar`: o PR reprova se ele estiver defasado). Fica na raiz de `docs/` porque o script e o workflow o leem nesse caminho. |
