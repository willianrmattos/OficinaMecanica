# Teste de stress (k6)

Gera carga simples contra a API para observar o `HorizontalPodAutoscaler`
(`k8s/oficinamecanica-api/hpa.yaml`, 2-5 réplicas por CPU/memória) escalando
em tempo real.

Bate direto em `GET /api/ordens-de-servico/numero/{numero}` — único
endpoint de leitura público (`[AllowAnonymous]`), não precisa de login. O
número usado nem precisa existir de verdade: mesmo respondendo `404`, o
handler já consulta o banco pra saber disso, o que já basta pra gerar carga
real de CPU/EF Core.

## Importante: `BASE_URL` — Ingress, não `kubectl port-forward`

```powershell
kubectl get svc -n ingress-nginx ingress-nginx-controller
```

`kubectl port-forward svc/oficinamecanica-api ...` prende a conexão inteira
a **um único pod** escolhido no início do comando — o tráfego do teste iria
sempre para esse mesmo pod, mesmo depois do HPA subir novas réplicas (elas
ficariam ociosas, sem receber nada). Batendo no IP do Ingress, o tráfego
passa pelo Service de verdade, que distribui entre todas as réplicas.

## Rodando

```powershell
k6 run -e BASE_URL="http://<ip-do-ingress>" stress/ordens-de-servico-stress.js
```

Variáveis de ambiente aceitas (`-e NOME=valor`):

| Variável | Default | O que é |
|---|---|---|
| `BASE_URL` | `http://localhost:8080` | Host da API (ver aviso acima) |
| `VUS` | `20` | Número de usuários virtuais simultâneos |
| `DURATION` | `2m` | Duração do teste |

## O que observar durante o teste (em outro terminal)

```powershell
kubectl get hpa oficinamecanica-api -w   # coluna TARGETS (uso atual/meta) e REPLICAS mudando ao vivo
kubectl get pods -l app=oficinamecanica-api -w   # novos pods sendo criados
kubectl top pods -l app=oficinamecanica-api      # uso de CPU/memoria por pod, no momento
```
