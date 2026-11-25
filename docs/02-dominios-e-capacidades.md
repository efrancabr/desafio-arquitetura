# 02 — Domínios Funcionais e Capacidades de Negócio

## Cadeia de valor do fluxo de caixa do comerciante

```
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│  REGISTRAR   │ -> │  CONSOLIDAR  │ -> │  REPORTAR    │
│  lançamento  │    │  saldo diário│    │  (consulta)  │
└──────────────┘    └──────────────┘    └──────────────┘
  assíncrono? NÃO      assíncrono? SIM      leitura pura
```

Três etapas com naturezas tecnológicas distintas — essa distinção é o primeiro passo da decomposição:
o registro é **síncrono e crítico**; a consolidação é um **efeito derivado** (pode atrasar); o relatório
é **leitura pura** (nunca escreve).

## Mapa de capacidades de negócio

| Capacidade | Nível | Domínio funcional | Descrição | Criticidade |
|---|---|---|---|---|
| Gestão de Lançamentos | Core | **Lançamentos** | Registrar débitos/créditos com idempotência | **Crítica** — se cair, o comerciante não registra |
| Consulta de Lançamentos | Supporting | **Lançamentos** | Listagem filtrada por período, auditoria | Alta |
| Consolidação de Saldo | Core | **Consolidado** | Agregar saldo diário por comerciante | Média — atraso tolerável |
| Relatório de Saldo | Supporting | **Consolidado** | Consulta do consolidado por período | Média — atraso tolerável |

## Bounded contexts (decomposição DDD)

### Contexto: Lançamentos (source of truth)

- **Responsabilidade:** validar, persistir e garantir a unicidade de cada lançamento; emitir o fato `LancamentoCriado`.
- **Linguagem ubíqua:** `lançamento`, `débito`, `crédito`, `idempotencyKey`, `valor`, `data`.
- **Invariantes:** valor > 0; data não futura; chave de idempotência única por comerciante; lançamento registrado nunca é alterado nem removido (append-only).

### Contexto: Consolidado (projeção)

- **Responsabilidade:** manter a projeção `saldos_diarios` (comerciante, data → totais) e servir o relatório.
- **Linguagem ubíqua:** `saldo do dia`, `total de créditos`, `total de débitos`, `período`.
- **Invariantes:** um saldo por (comerciante, data); aplicação idempotente do mesmo evento; saldo = créditos − débitos.

## Context map

| | Lançamentos | Consolidado |
|---|---|---|
| **Lançamentos** | — | upstream: emite `LancamentoCriado` (relação *Partnership* — contrato versionado, sem dependência síncrona) |
| **Consolidado** | downstream: consome e projeta | — |

**Regra do mapa:** o único acoplamento permitido entre os dois contextos é o **evento de domínio
publicado no broker**. Nenhuma chamada HTTP/gRPC cruzada. Isso torna o mapa resistente: nenhum dos
lados pode derrubar o outro (RNF-01 satisfeito por construção).

## Responsabilidades operacionais (quem faz o quê)

| Atividade | Dono | Observação |
|---|---|---|
| Aceitar pedido de lançamento | API Lançamentos | valida → grava → publica (via outbox) |
| Garantir entrega do fato | Outbox Publisher (inside API) | at-least-once; retorna 201 antes da publicação no broker |
| Aplicar fato no saldo | Worker Consolidado | ack manual, dedup por MessageId |
| Servir relatório | API Consolidado | read-only, O(1) por dia |

## Como a decomposição atende a Arquitetura Corporativa

A governança do sistema estabelece a definição de *processos, etapas e responsabilidades de forma isolada ou integrada*:

- **Isoladas:** registro e consolidação rodam em processos/serviços distintos (isolamento de falha, escala independente).
- **Integradas:** via contrato de eventos versionado — o ponto de integração é um artefato governado, não uma chamada ad hoc.
- **Escala de evolução:** novos contextos (conciliação, notificações, BI) entram como **novos consumidores** do mesmo exchange, sem tocar nos serviços existentes, garantindo extensibilidade arquitetural.

## Decisão de fronteira física

Para a volumetria inicial projetada, um banco PostgreSQL com fronteira **lógica** por tabela (cada contexto
só acessa as suas). A fronteira é código de composição: separar em bancos distintos futuramente custa
mudar uma connection string, não redesenhar (ver ADR-003).
