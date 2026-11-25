# 08 — Observabilidade, Monitoramento e Custos

## Observabilidade

### Tríade de telemetria

| Sinal | Ferramenta | Detalhe |
|---|---|---|
| Métricas | Prometheus + Grafana | aplicação (rps, latência, erros) + broker (profundidade, taxa) + banco |
| Logs | estruturados (JSON), coleta central | correlation ID propagado: `idempotencyKey` → `lancamentoId` → `MessageId` |
| Traces | OpenTelemetry (AspNet + Http) | trace do POST até o consumer — o que distingue debug de evento |

### Painéis e alertas (as 4 linhas de defesa viram números)

| Alerta | Condição | Ação |
|---|---|---|
| Outbox atrasado | pendentes > 50 por 5 min | investigar broker |
| Fila crescendo | depth > 1.000 por 10 min | investigar worker |
| **DLQ não vazia** | **qualquer mensagem** | **página imediata — reprocessamento manual** |
| Latência de registro | p95 > 200 ms por 5 min | investigar banco/carga |
| Erros 5xx | qualquer ocorrência | incidente |

### SLI/SLO (bússola do time)

| Serviço | SLO |
|---|---|
| API Lançamentos | 99,9% disponível · p95 < 200 ms · erro 5xx ≈ 0 |
| Consolidado (read + worker) | frescor p95 < 10 s · DLQ = 0 · backlog < 5 s |

## Estimativa de custos (FinOps)

Premissas: ~1.000 comerciantes, 50 rps de pico no consolidado, ~200 mil lançamentos/dia. Valores em USD/mês, regime gerenciado — **com busca ativa do mínimo que atende os SLOs**.

### Opção A — Cloud gerenciada (produção), o mesmo workload em duas clouds

Serviços equivalentes têm custo semelhante entre GCP e AWS; a tabela mantém a equivalência explícita —
a arquitetura não amarra a nenhuma delas:

| Item | GCP | AWS | Custo/mês (ambas) |
|---|---|---|---|
| Kubernetes | GKE Autopilot | EKS (Fargate) | $ 80–120 |
| PostgreSQL gerenciado | Cloud SQL (HA multi-zona) | RDS for PostgreSQL (Multi-AZ) | $ 100–150 |
| RabbitMQ gerenciado | CloudAMQP (marketplace) | Amazon MQ for RabbitMQ (3 nós) | $ 50–100 |
| Métricas/logs/traces | Cloud Monitoring + Grafana | CloudWatch + Grafana | $ 0–50 |
| **Total** | **GCP: ~$ 230–420** | **AWS: ~$ 230–420** | |

### Opção B — Mínima viável (piloto/validação)

2 VMs dedicadas (apps+worker; banco+broker) + backup: **~$ 90/mês** — sem HA gerenciada; adequada a piloto.

### Licenças

**$ 0.** .NET (MIT) · PostgreSQL (PostgreSQL License) · RabbitMQ (MPL) · demais libs (MIT/Apache).

### Cultura de custo embutida

- Escala elástica dirigida por métrica (não por horário): HPA por rps; KEDA por profundidade de fila.
- Right-sizing trimestral; ambientes não-produtivos desligados fora de uso.
