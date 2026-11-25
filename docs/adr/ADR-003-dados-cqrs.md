# ADR-003 — Dados: PostgreSQL único com CQRS no nível de tabela

**Status:** Aceito

## Contexto

Dois modelos com cargas opostas: escrita transacional de lançamentos (crítica) e leitura agregada de
saldos (analítica, 50 rps). Volume não justifica infraestrutura distribuída.

## Decisão

PostgreSQL 16 único, fronteira lógica por tabela (`lancamentos`+`outbox` de um lado; `saldos`+`processed`
do outro), aplicando **CQRS**: a escrita nunca consulta o read model, a leitura nunca escreve.

## Racional — o que o CQRS resolve de verdade aqui

Sem read model, cada relatório faria `SUM()` sobre lançamentos do período: O(n) por consulta competindo
com a escrita. Com `saldos_diarios` projetado por evento, cada dia custa **O(1)**.

## Por que banco único (trade-off explícito)

O outbox **precisa** da mesma transação do lançamento (ADR-004). Separar bancos nesta carga traria
coordenação distribuída no ponto mais crítico sem ganho de escala. A fronteira é código de composição:
partir para banco por serviço depois = trocar uma connection string.

## Alternativas descartadas

- Bancos separados agora: coordenação do outbox ficaria distribuída (não aceitável).
- SQL Server/Oracle: custo de licença sem requisito de feature.
- NoSQL para saldos: agregação financeira precisa de `decimal` exato e transação — SQL relacional é o certo.
