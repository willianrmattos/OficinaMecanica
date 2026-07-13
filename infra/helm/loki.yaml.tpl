# Rodei em modo Monolithic (nome atual do antigo "SingleBinary" - so o
# necessario pra um volume pequeno de logs, sem os componentes read/write/
# backend separados do modo distribuido, que fariam sentido em escala maior).
deploymentMode: Monolithic

# O chart nao zera sozinho os componentes do modo Simple Scalable so por
# causa do deploymentMode acima - preciso zerar explicitamente, senao ele
# recusa instalar reclamando de replicas > 0 em dois modos ao mesmo tempo.
read:
  replicas: 0
write:
  replicas: 0
backend:
  replicas: 0

loki:
  auth_enabled: false

  # Configurei replication_factor: 1 porque so existe 1 replica do
  # singleBinary (abaixo) - o default do chart e 3, pensado pro modo
  # distribuido com varios nodes.
  commonConfig:
    replication_factor: 1

  # Optei por filesystem (sem object storage, ex: Azure Blob Storage):
  # armazenamento no PVC do proprio pod. bucketNames sao só nomes de
  # diretorio nesse modo, nao buckets de verdade - mas o chart exige que
  # sejam informados mesmo assim.
  storage:
    type: filesystem
    bucketNames:
      chunks: chunks
      ruler: ruler
      admin: admin

  # Usei tsdb + v13, o schema atual recomendado pelo proprio Loki (versoes
  # anteriores de schema/index sao mantidas so por compatibilidade com
  # instalacoes antigas, sem motivo pra usar aqui numa instalacao nova).
  schemaConfig:
    configs:
      - from: "2024-01-01"
        store: tsdb
        object_store: filesystem
        schema: v13
        index:
          prefix: loki_index_
          period: 24h

  limits_config:
    retention_period: 72h

  # delete_request_store e obrigatorio a partir do Loki 3.x quando a
  # retencao esta ligada (o compactor precisa de um lugar pra guardar os
  # pedidos de delete pendentes) - apontei pro mesmo store filesystem
  # configurado acima, ja que nao tem object storage nesta instalacao.
  compactor:
    retention_enabled: true
    delete_request_store: filesystem

singleBinary:
  replicas: 1

  # Declarei recursos explicitos (sem confiar no default do chart, mesmo
  # estilo ja usado pro Prometheus/Grafana): sem limite de CPU, só de memoria.
  resources:
    requests:
      cpu: 100m
      memory: 256Mi
    limits:
      memory: 512Mi

  persistence:
    enabled: true
    size: 10Gi
    storageClass: ${storage_class_name}

# Desabilitei o gateway (nginx na frente do Loki) - sem necessidade de
# multi-tenant ou autenticacao extra, o Grafana (e o Alloy) falam direto com
# o singleBinary na porta 3100.
gateway:
  enabled: false

# Desabilitei o MinIO - so seria necessario se eu estivesse usando object
# storage em vez do filesystem local configurado acima.
minio:
  enabled: false

# Desabilitei os caches Memcached de chunks/resultados de query pra manter o
# footprint minimo no node unico do cluster - cada um adicionaria outro
# StatefulSet consumindo CPU/memoria
chunksCache:
  enabled: false
resultsCache:
  enabled: false

# O canary escreve e le logs de teste continuamente pra verificar que o
# pipeline esta funcionando - util, mas e mais um pod rodando o tempo todo
# (DaemonSet). Desabilitei por enquanto.
lokiCanary:
  enabled: false

# O "helm test" embutido no chart depende do canary acima pra verificar o
# pipeline de ponta a ponta - como desabilitei o canary, preciso desabilitar
# o test tambem, senao o proprio chart recusa instalar ("Helm test requires
# the Loki Canary to be enabled").
test:
  enabled: false
