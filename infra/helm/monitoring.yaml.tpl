# Desabilitei esses monitores porque no AKS o control plane e gerenciado pela
# Azure e nao e exposto da forma que eles esperam - ficariam permanentemente
# "down", gerando apenas ruido.
kubeControllerManager:
  enabled: false
kubeScheduler:
  enabled: false
kubeEtcd:
  enabled: false
kubeProxy:
  enabled: false

# Deixei o Alertmanager desabilitado: ainda nao configurei nenhum alerta
# (Slack/e-mail), e nao justifica o consumo adicional de recurso no node
# unico para rodar um componente ocioso.
alertmanager:
  enabled: false

prometheus:
  prometheusSpec:
    retention: 6h
    resources:
      requests:
        cpu: 150m
        memory: 384Mi
      limits:
        memory: 768Mi
    storageSpec:
      volumeClaimTemplate:
        spec:
          storageClassName: ${storage_class_name}
          accessModes: ["ReadWriteOnce"]
          resources:
            requests:
              storage: 8Gi

grafana:
  # Serve atras do Azure API Management (infra/apim/, path "/grafana") -
  # sem isso, links/redirects que o proprio Grafana gera internamente
  # (login, assets estaticos) saem sem o prefixo "/grafana", quebrando a
  # navegacao no browser quando acessado via APIM (o acesso direto via
  # ingress-nginx, sem prefixo, continua funcionando normalmente de
  # qualquer forma - esse root_url so afeta como o Grafana MONTA seus
  # proprios links, nao onde ele aceita conexao).
  #
  # serve_from_sub_path = false (nao true) de proposito: essa flag e pra
  # quando o proprio Grafana precisa REMOVER o prefixo das requests que
  # recebe (reverse proxy so repassa, sem reescrever nada). Aqui e o
  # contrario - a policy da APIM (infra/apim/) ja tira o "/grafana" antes
  # de encaminhar pro backend (confirmado testando /grafana/api/health),
  # entao o Grafana recebe os paths sem prefixo mesmo, so precisa do
  # root_url certo pra montar os links de volta.
  "grafana.ini":
    server:
      root_url: "https://${apim_gateway_host}/grafana/"
      serve_from_sub_path: false
  persistence:
    enabled: true
    storageClassName: ${storage_class_name}
    size: 4Gi
  resources:
    requests:
      cpu: 50m
      memory: 128Mi
    limits:
      memory: 256Mi
  # Tive um crash loop do Grafana no primeiro boot: o default do chart
  # (failureThreshold: 10, period: 10s) da apenas ~160s de tolerancia apos o
  # delay inicial de 60s, insuficiente para a primeira inicializacao, quando
  # o Grafana executa centenas de migrations no banco interno antes de
  # responder em /api/health. Sem essa folga, o liveness probe matava o
  # container no meio da migracao; o container reiniciava e retomava de onde
  # parou (o progresso persiste no PVC), mas entrava em loop ate eventualmente
  # concluir dentro de uma janela. Ampliei a tolerancia para ~6 minutos
  # (60s + 30 * 10s) para cobrir esse cenario de forma deterministica.
  livenessProbe:
    httpGet:
      path: /api/health
      port: 3000
    initialDelaySeconds: 60
    timeoutSeconds: 30
    periodSeconds: 10
    failureThreshold: 30
  # Configurei dashboards como codigo: o sidecar monitora ConfigMaps com o
  # label abaixo em qualquer namespace e os importa automaticamente no
  # Grafana. A fonte da verdade passa a ser o ConfigMap versionado
  # (k8s/monitoring/), nao o que foi criado manualmente na UI - o conteudo
  # sobrevive a perda do disco/PVC.
  sidecar:
    dashboards:
      enabled: true
      label: grafana_dashboard
      labelValue: "1"
      searchNamespace: ALL

  # Loki (infra/helm/loki.tf) como fonte de dados de logs, direto pelo nome
  # DNS interno do Service (mesmo namespace "monitoring") - sem gateway na
  # frente, entao aponto direto pra porta 3100 do singleBinary.
  additionalDataSources:
    - name: Loki
      type: loki
      access: proxy
      url: http://${loki_release_name}.${monitoring_namespace}.svc.cluster.local:3100
    # Jaeger (k8s/jaeger/) - nao e um helm_release (Deployment/Service
    # aplicados direto via k8s/)
    - name: Jaeger
      type: jaeger
      access: proxy
      url: http://jaeger.monitoring.svc.cluster.local:16686
