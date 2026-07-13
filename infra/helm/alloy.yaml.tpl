alloy:
  # Escrevi uma config minima em River (linguagem propria do Alloy): descubro
  # os pods do proprio node, monto o caminho do arquivo de log de cada
  # container e envio tudo pro Loki. Sem nenhum processamento/parsing
  # adicional - so tailing + push.
  configMap:
    content: |
      discovery.kubernetes "pods" {
        role = "pod"
      }

      discovery.relabel "pods" {
        targets = discovery.kubernetes.pods.targets

        rule {
          source_labels = ["__meta_kubernetes_namespace"]
          target_label  = "namespace"
        }
        rule {
          source_labels = ["__meta_kubernetes_pod_name"]
          target_label  = "pod"
        }
        rule {
          source_labels = ["__meta_kubernetes_pod_container_name"]
          target_label  = "container"
        }
        rule {
          source_labels = ["__meta_kubernetes_pod_uid", "__meta_kubernetes_pod_container_name"]
          separator     = "/"
          target_label  = "__path__"
          replacement   = "/var/log/pods/*$1/*.log"
        }
      }

      // local.file_match e quem de fato expande o glob do __path__ acima
      // (ex: "/var/log/pods/*<uid>/<container>/*.log") em arquivos reais do
      // disco, reconferindo periodicamente. Sem esse passo, o loki.source.file
      // tenta abrir o glob como se fosse um caminho literal (com os "*" e
      // tudo) e falha - foi exatamente esse erro que apareceu nos logs do
      // Alloy na primeira tentativa ("stat failed: ... no such file or
      // directory" pra todo pod, mesmo os que tem log de verdade).
      local.file_match "pods" {
        path_targets = discovery.relabel.pods.output
      }

      loki.source.file "pods" {
        targets    = local.file_match.pods.targets
        forward_to = [loki.write.default.receiver]
      }

      loki.write "default" {
        endpoint {
          url = "${loki_push_url}"
        }
      }

  # Monta /var/log do node no container (onde ficam os arquivos de log de
  # cada pod/container) - sem isso, a config acima nao alcanca nenhum log.
  mounts:
    varlog: true

  # Recursos explicitos, mesmo estilo do restante do modulo: sem limite de
  # CPU, so de memoria. Bem menor que o Loki - o Alloy so le e encaminha,
  # nao processa nem armazena nada.
  resources:
    requests:
      cpu: 50m
      memory: 64Mi
    limits:
      memory: 128Mi
