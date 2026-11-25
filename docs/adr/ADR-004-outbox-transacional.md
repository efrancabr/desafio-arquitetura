# ADR-004 — Publicação de eventos: Transactional Outbox

**Status:** Aceito

## Contexto

Registrar um lançamento exige **duas** verdades: o dado persistido e o fato notificado ao consolidado.
Elas não podem divergir — "gravou sem notificar" congela o saldo para sempre; "notificou sem gravar"
soma valor inexistente.

## Alternativas

| Alternativa | Falha no cenário |
|---|---|
| Publicar direto no request | broker cai no meio do POST → evento perdido → perda > 0% permanente |
| Dual-write + compensação | sem 2PC há janela de inconsistência; código compensatório é a pior classe de bug financeiro |
| CDC (Debezium) | correto, mas exige Kafka Connect — complexidade e custo de infra desproporcionais para a volumetria |
| **Outbox transacional** | falha projetada: evento fica no banco até publicar |

## Decisão

Lançamento + evento gravados na **mesma transação** (tabela `outbox_messages`); um
`OutboxPublisher` (BackgroundService, tick 1 s) drena pendências para o broker e marca `Processado`.

## Consequências

- **Garantia:** 201 na API ⇒ evento existe e será publicado (at-least-once) — RNF-03 e RNF-01 atendidos juntos.
- **Custo:** latência de até 1 s a mais na projeção (irrelevante para consolidação diária) e uma tabela a manter.
- **Caveat registrado:** se o publisher morrer após publicar e antes de marcar `Processado`, o evento é republicado — caso coberto pela deduplicação do consumidor (ADR-005).
