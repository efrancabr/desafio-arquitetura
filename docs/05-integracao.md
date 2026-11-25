# 05 — Integração entre Serviços e Sistemas

## Matriz de integração

| Origem → Destino | Estilo | Protocolo | Formato | Ferramenta |
|---|---|---|---|---|
| Cliente/PDV → API Lançamentos | Síncrono (request/response) | HTTPS | JSON (REST) | ASP.NET Core controllers |
| Cliente → API Consolidado | Síncrono (request/response) | HTTPS | JSON (REST) | ASP.NET Core controllers |
| Lançamentos → Consolidado | **Assíncrono (evento)** | AMQP 0-9-1 | JSON | RabbitMQ + outbox |

**Princípio:** síncrono onde o cliente espera resposta no ato (registro, consulta); assíncrono onde o
produtor não deve depender do consumidor (projeção de saldo). Nunca sincronia entre os dois domínios.

## Contrato de evento (o artefato mais governado da solução)

`LancamentoCriado` — publicado no exchange `fluxocaixa.events`:

```json
{
  "lancamentoId": "guid",
  "comercianteId": "guid",
  "data": "2026-11-23",            // yyyy-MM-dd
  "tipo": 1,                        // 1=Débito, 2=Crédito (enum int, não string localizada)
  "valor": 100.55,                  // decimal(18,2)
  "descricao": "venda cartão",
  "ocorridoEmUtc": "2026-11-23T14:05:11Z"
}
```

Regras de governança do contrato:
1. **Compatibilidade:** novas versões só adicionais (campo novo nunca quebra consumidor existente).
2. **Enums numéricos + descrição em documentação** — nunca strings localizadas dentro do contrato.
3. **Sempre UTC + timezone explícito** para dados financeiros; `data` é data de negócio, não timestamp.
4. **MessageId obrigatório** (id do outbox) — a base da deduplicação (ADR-005).
5. **Payload versionado** quando houver breaking change: campo `versao` + roteamento por fila versionada.

## Padrões de integração aplicados

| Padrão | Onde | Por quê |
|---|---|---|
| Transactional Outbox | Lançamentos | Garantia de publicação (ver ADR) |
| At-least-once + consumidor idempotente | Broker → Worker | Entrega confiável sem transação distribuída |
| Competing Consumers | Worker (réplicas) | Escala horizontal de consumo |
| Dead Letter Channel | RabbitMQ DLQ | Desacoplar falha irrecuperável do caminho feliz |
| Anti-Corruption Layer | Futuras integrações externas | Caso surjam provedores externos, blindar o domínio |

## Ferramentas consideradas (matriz de escolha — ver ADR-002)

| Critério | RabbitMQ | Kafka | Solução custom (fila em tabela) |
|---|---|---|---|
| Carga 50 rps | sobra | sobra | atende |
| DLQ nativa | sim | não (custom) | não |
| Retry/requeue | sim, nativo | custom | custom |
| Ops em 1 nó | sim, simples | cluster obrigatório | só banco |
| Replay longo/polystream | não | sim | não |

**Decisão:** RabbitMQ. O único recurso exclusivo do Kafka (replay/streaming analítico) é coberto pelo
outbox no PostgreSQL — duplicar o canal seria complexidade sem requisito.

## Contratos de API (resumo REST)

| Verbo/rota | Entrada | Saída |
|---|---|---|
| `POST /api/lancamentos` | corpo do lançamento + `idempotencyKey` | 201 (novo) / 200 (idempotente) / 400 (inválido) |
| `GET /api/lancamentos?comercianteId&inicio&fim&pagina&tamanho` | query | página de lançamentos + total |
| `GET /api/consolidado/{comercianteId}?inicio&fim` | path + query | saldo por dia + total do período |

Semântica HTTP rigorosa: 201 cria, 200 idempotência, 400 validação, 404 recurso inexistente,
429 rate limit (futuro), 503 apenas quando o próprio serviço estiver degradado — e nunca por culpa do consolidado.
