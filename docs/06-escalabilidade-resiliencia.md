# 06 — Escalabilidade e Resiliência

## Escalabilidade

### Perfil de carga (o ponto de partida — dimensionar contra números, não sensações)

| Caminho | Carga declarada | Projeção 12 meses (3×) | Estratégia |
|---|---|---|---|
| Registro de lançamento | não declarado (implícito ≤ consolidado) | ~150 rps | Horizontal stateless |
| Consulta consolidado | **50 rps pico** | ~150 rps | Read model + escala + cache de leitura |
| Consumo de eventos | ~50 msg/s pico | ~150 msg/s | Competing consumers |

### Estratégias por camada

| Camada | Técnica | Justificativa para ESTE problema |
|---|---|---|
| API Lançamentos | Horizontal (N réplicas atrás de LB) | Stateless por natureza; sessão nenhuma |
| API Consolidado | Horizontal + query ao read model | O(1) por dia já é a otimização estrutural |
| Worker | Competing consumers (N réplicas, prefetch baixo) | Escala de consumo sem mudar topologia |
| Aplicação | Cache seletivo | Só onde não viola correção financeira (ver abaixo) |
| Banco | Réplica de leitura para consultas (fase 2) | Separa carga analítica da escrita |
| Broker | Fila durável + DLX | Amortece picos sem perda |

**Cache — onde e onde NÃO:** relatório consolidado é dado financeiro; cache com TTL curto (5–15 s)
é o máximo seguro e mesmo assim dispensável a 50 rps (o read model atende sem cache). O desenho
**não usa cache para lançamentos** — nunca se serve saldo/lançamento de cache quando a moeda
perdida em frescor não se recupera.

2 réplicas por API + 2 worker em produção atendem o cenário com
folga 2×. Dimensionar além disso é desperdício FinOps — o escalonamento é elástico via HPA/KEDA
(base: métrica de requisição para APIs; profundidade de fila para o worker — escala dirigida pelo
dado certo).

## Resiliência

### As 4 linhas de defesa (do risco imediato ao extremo)

| # | Defesa | Falha que cobre |
|---|---|---|
| 1 | Transactional Outbox | Broker indisponível ⇒ evento retido no banco, zero perda |
| 2 | At-least-once + dedup por MessageId | Reentrega/republicação ⇒ nenhuma duplicidade |
| 3 | Retry com contagem (3×) + requeue | Falha transitória no worker ⇒ reprocessa |
| 4 | DLQ monitorada | Falha irrecuperável ⇒ auditável, alerta, reprocessamento manual |

### Comportamento sob falha (matriz de cenários)

| Cenário | Efeito no registro | Efeito no consolidado | Recuperação |
|---|---|---|---|
| Consolidated Worker cai | **nenhum** (RNF-01) | saldo congela (não corrompe) | worker volta e drena backlog |
| RabbitMQ cai | **nenhum** (outbox retém) | fila volta a receber | reconexão automática (backoff) |
| PostgreSQL cai | indisponível (crítico, como deve ser) | indisponível | failover da réplica (se HA) / restore DR |
| API Consolidado cai | **nenhum** | consulta indisponível | novo deploy (trade-off aceito: 99,5%) |
| Pico ≥ 2× declarado | degrade via fila (nunca perde) | backlog temporário | escala elástica |

**Trade-off explícito:** disponibilidade multi-região para o consolidado custaria múltiplos da
solução sem agregar valor correspondente, dado que o consolidado tolera consistência eventual.
Alta disponibilidade com redundância ativa é priorizada onde o requisito crítico a exige (lançamentos, com RTO ≤ 30 min).

### Redundância e failover

- **Aplicações:** N réplicas stateless; health checks (`/health`) no LB; readiness separada de liveness.
- **Broker:** fila durável + políticas de HA (quorum queue em produção); conexão com `AutomaticRecoveryEnabled`.
- **Banco:** backups incrementais com teste de restore periódico (RPO ≤ 5 min, RTO ≤ 30 min); réplica de leitura quando a carga analítica justificar.

### Recuperação de desastres

| Atributo | Meta | Mecanismo |
|---|---|---|
| RPO | ≤ 5 min | backup incremental contínuo (PITR) |
| RTO | ≤ 30 min | restore + infra declarativa (Compose hoje; IaC em produção) |
| Teste | trimestral | restore em ambiente descartável com validação de saldo |

### Monitoramento proativo (resumo — detalhe no doc 08)

Alertas com números de gatilho: outbox pendente > 50 por 5 min; fila depth > 1.000 por 10 min; **DLQ > 0 = página**; p95 > 200 ms por 5 min.
