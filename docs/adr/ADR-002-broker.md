# ADR-002 — Broker: RabbitMQ (e não Kafka)

**Status:** Aceito

## Contexto

O desacoplamento exige um broker para o fluxo `LancamentoCriado`: 50 rps de pico, um consumidor
dedicado, mensagens pequenas, entrega confiável com retry e DLQ.

## Avaliação

| Critério | RabbitMQ | Kafka |
|---|---|---|
| Carga | sobra (milhares/s) | sobra |
| DLQ | **nativa** (DLX) | construir manualmente |
| Retry/requeue | nativo | construir manualmente |
| Operação mínima | 1 nó | cluster (KRaft) |
| Replay/streaming | não | sim |

## Decisão

RabbitMQ 3.x, exchange durável + fila dedicada com DLX.

**Racional:** Kafka oferece técnicas que este problema não pede (replay longo, polystream analítico) e
cobra complexidade que ele não comporta (cluster, retry feito à mão). O replay que um dia poderia ser
necessário **já existe** como outbox no PostgreSQL — duplicar esse canal seria complexidade sem requisito
(princípio 2: simplicidade suficiente).

## Consequências

- Se surgir cenário de streaming analítico (diferente de um consumidor), a porta `IEventPublisher` isola o broker e a troca vira adaptador — dívida não assumida.
- Nota técnica de implementação: com `RabbitMQ.Client` 6.x e consumidor assíncrono, `ConnectionFactory.DispatchConsumersAsync = true` é obrigatório (o dispatcher síncrono descarta entregas silenciosamente — armadilha documentada para a equipe).
