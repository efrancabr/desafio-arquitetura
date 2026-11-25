# 03 — Refinamento de Requisitos (Funcionais e Não-Funcionais)

## Requisitos funcionais refinados

| ID | Requisito | Critério de aceitação |
|---|---|---|
| RF-01 | Registrar lançamento | Comerciante, data, tipo (débito/crédito), valor, descrição opcional, chave de idempotência. Retorna 201 com identificador do lançamento. |
| RF-02 | Idempotência de registro | Reenvio com a mesma chave retorna o lançamento original (200), sem duplicar. |
| RF-03 | Listar lançamentos | Por comerciante, com filtro de período e paginação (padrão 50, máx. 200). |
| RF-04 | Consolidar saldo diário | Saldo por comerciante/dia = total de créditos − total de débitos, atualizado a cada lançamento (eventual). |
| RF-05 | Relatório consolidado | Por comerciante, período arbitrário; resposta inclui saldo por dia + total do período. |
| RF-06 | Validação de invariantes | Valor > 0 (máx 2 casas), data ≤ hoje+1, descrição ≤ 200, chave 8–128 caracteres. Violação = 400 com detalhe. |

**Edge cases cobertos pelo desenho:**
- Dois lançamentos no mesmo segundo com a mesma chave ⇒ o índice único decide; o perdedor devolve 200 idempotente (ver ADR-006).
- Virada de dia ⇒ o saldo se separa naturalmente por (comerciante, data) — sem job de fechamento.
- Relatório de período sem lançamentos ⇒ dias com zero não são retornados; saldo total = 0. (Decisão: não materializar dias vazios — custo de armazenamento vs. conveniência de leitura.)

## Requisitos não-funcionais refinados (com metas)

Fonte: Especificação de negócio + inferências de domínio financeiro. Cada meta é **mensurável** — sem meta, RNF é opinião.

| ID | Origem | Requisito | Meta (SLO) | Como é atendido |
|---|---|---|---|---|
| RNF-01 | Negócio | Lançamentos não pode cair se o consolidado cair | Disponibilidade de lançamentos = 99,9% **independente** do consolidado | Zero chamada síncrona entre serviços (eventos + outbox) |
| RNF-02 | Negócio | Pico de 50 rps no consolidado | Capacidade sustentada ≥ 100 rps (2× folga); fila drenada em < 5s após pico | Consumidor com prefetch, escala horizontal do worker |
| RNF-03 | Negócio | Máx. 5% de perda/atraso em picos | **Perda efetiva alvo: 0%** para falhas transitórias; DLQ auditável | Outbox → at-least-once → retry 3× → DLQ monitorada |
| RNF-04 | Inferido | Latência de registro | p95 < 200 ms; p99 < 500 ms | 2 INSERTs em transação local; sem I/O de rede no critical path |
| RNF-05 | Inferido | Frescor do consolidado | p95 < 10 s entre lançamento e saldo visível | Pipeline assíncrono (outbox tick 1s + consumo ~ms) |
| RNF-06 | Inferido | Consistência financeira | Nenhum lançamento contabilizado duas vezes nem perdido | Idempotência dupla (API + consumidor); decimal(18,2) ponta a ponta |
| RNF-07 | Inferido | Segurança dos dados | Dados criptografados em trânsito e em repouso; acesso mínimo | TLS; usuários por serviço; princípio do menor privilégio |
| RNF-08 | Inferido | Recuperação de desastres | RPO ≤ 5 min; RTO ≤ 30 min (lançamentos) | Backups incrementais, restore testado |

### Leitura crítica do RNF-03 (tolerância a falhas transitórias ≤ 5%)

Embora o SLA de negócio preveja uma tolerância de até 5% de falhas transitórias em picos extremos, o desenho arquitetural estabelece uma meta de **0% de perda efetiva**: o desenho converte falhas transitórias em atraso controlado de projeção. Pedido aceito ⇒ lançamento persistido em transação local ⇒ fato registrado no outbox ⇒ a única consequência temporária é **atraso na projeção do consolidado**, que se resolve automaticamente quando o worker consome a fila. Perda definitiva só ocorreria com falha simultânea de banco E broker após o pedido ser aceito — cenário para o qual há recuperação por DR (RNF-08). Conclusão: atende-se o requisito com alta folga **(0% efetivo contra 5% tolerado).**

### SLOs consolidados por serviço

| Serviço | Disponibilidade | Latência | Outros |
|---|---|---|---|
| API Lançamentos | 99,9% | p95 < 200 ms | Erros 5xx ≈ 0 |
| API Consolidado | 99,5% (pode cair — por design) | p95 < 300 ms | Frescor p95 < 10 s |
| Worker Consolidado | — | backlog drenado < 5 s | DLQ = 0 (alerta imediato) |
