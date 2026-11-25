# ADR-005 — Idempotência dupla: chave de negócio + MessageId

**Status:** Aceito

## Contexto

At-least-once existe em duas pontas: o cliente pode reenviar o POST (retry de rede, duplo clique) e o
broker pode reentregar mensagens (recovery). Nenhum lançamento pode ser contabilizado duas vezes.

## Decisão — duas camadas, porque são dois problemas

| Camada | Mecanismo | Protege contra |
|---|---|---|
| API (`idempotencyKey`) | **índice UNIQUE** `(comercianteId, idempotencyKey)` | reenvio do cliente; reenvio responde 200 com o original |
| Consumer (`MessageId`) | tabela `processed_messages` com PK = MessageId, gravada na mesma transação do saldo | reentrega do broker |

## Por que não "exactly-once"

Exactly-once entre sistemas distintos não existe sem transação distribuída — quem o promete engana.
O que se entrega é **at-least-once com deduplicação** = effectively-once, com o bônus da honestidade:
o mecanismo é verificável nos testes.

## Consequências

- **Caveat registrado:** dois requests simultâneos com a mesma chave — o índice único decide (um vence, o outro deve ser tratado com re-busca e 200; está mapeado como item de evolução o tratamento gracioso da violação da UK em vez de erro 500).
- Retry-count do consumidor em memória (perdido em restart): o piso continua seguro — requeue re-executa e a dedup absorve; persistir a contagem (quorum queue + TTL) é evolução documentada.
