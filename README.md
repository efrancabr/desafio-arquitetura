# Fluxo de Caixa — Arquitetura de Referência e Plataforma de Lançamentos e Consolidado Diário

> **Autor:** Emerson França da Silva — Enterprise / Solutions Architect  
> **Stack de referência:** .NET 8 (C#) · PostgreSQL 16 · RabbitMQ 3.13 · Docker / Docker Compose · xUnit + Testcontainers · OpenTelemetry  
> **Abordagem:** TOGAF (método) + DDD (bounded contexts) + C4 (representação) + Clean Architecture pragmática  

Este repositório contém a especificação de arquitetura corporativa e a implementação de referência para controle de fluxo de caixa diário e consolidação de saldos:
1. **Especificação Arquitetural Completa**: Desenho corporativo em profundidade (9 capítulos, 8 Architecture Decision Records formais e modelo C4 em 4 níveis + arquitetura de transição + runtime sob falha).
2. **Implementação de Referência (.NET 8)**: Solução funcional desacoplada via mensageria assíncrona, Transactional Outbox, CQRS e idempotência ponta a ponta.
3. **Bateria de Testes**: 13 testes unitários de domínio/casos de uso + teste de integração E2E com PostgreSQL e RabbitMQ reais via Testcontainers (14/14 aprovados).

---

## 🧭 Como navegar na documentação

| # | Documento | Conteúdo e Escopo |
|---|---|---|
| 01 | [Visão Geral e Método](docs/01-visao-geral.md) | Objetivo, abordagem, princípios de decisão técnica |
| 02 | [Domínios e Capacidades](docs/02-dominios-e-capacidades.md) | Mapeamento de domínios funcionais, value chain e capacidades de negócio |
| 03 | [Requisitos Funcionais e Não-Funcionais](docs/03-requisitos.md) | Refinamento com critérios de aceitação e SLOs numéricos |
| 04 | [Arquitetura Alvo](docs/04-arquitetura-alvo.md) | Desenho completo da solução e fluxo de dados |
| 05 | [Integração entre Serviços](docs/05-integracao.md) | Comunicação, protocolos, formatos e contratos de eventos |
| 06 | [Escalabilidade e Resiliência](docs/06-escalabilidade-resiliencia.md) | Alta disponibilidade, desempenho e recuperação de falhas em 4 camadas |
| 07 | [Segurança](docs/07-seguranca.md) | Autenticação, autorização, criptografia, ameaças e critérios de consumo |
| 08 | [Observabilidade e Custos](docs/08-observabilidade-custos.md) | Monitoramento (SLI/SLO) + FinOps com estimativas de infraestrutura |
| 09 | [Arquitetura de Transição](docs/09-arquitetura-de-transicao.md) | Estratégia de migração de legado (Strangler Fig) com paridade de dados |
| — | [ADRs (Architecture Decision Records)](docs/adr/) | 8 decisões justificadas com contexto e trade-offs explícitos |
| — | [Diagramas C4 (Visualizador Interativo)](c4/arquitetura-c4.html) | Modelo C4 em 4 níveis + transição + runtime sob falha |
| — | [Diagramas C4 (PDF para Apresentação)](c4/arquitetura-c4.pdf) | Versão A4 paisagem formatada para leitura executiva |

---

## 📌 Decisão central de arquitetura

Requisito Não-Funcional Central (Isolamento de Domínio e Alta Disponibilidade):
> *"O serviço de controle de lançamento não deve ficar indisponível se o sistema de consolidado diário cair. Em dias de pico, o consolidado recebe 50 requisições por segundo, com no máximo 5% de tolerância a atraso/perda em picos extremos."*

Para o serviço de lançamentos **nunca** depender do consolidado, os dois serviços não possuem acoplamento síncrono. A solução adota comunicação puramente orientada a eventos (**Event-Driven**) com **Transactional Outbox**:

```
┌─────────────┐   HTTP    ┌──────────────────┐   transação    ┌──────────────┐
│  Comerciante │ ───────▶ │  API Lançamentos  │ ─────────────▶ │  PostgreSQL   │
└─────────────┘           │  (Outbox local)   │               │ (lancamentos + │
                          └────────┬─────────┘               │  outbox_events)│
                                   │ publica (at-least-once)  └──────────────┘
                                   ▼                                ▲
                          ┌──────────────────┐   consome + ack     │
                          │    RabbitMQ      │ ────────────────────┤
                          │  (+ DLQ p/ retry)│                     │
                          └────────┬─────────┘               ┌────┴───────────┐
                                   │ consome                  │     Worker     │
                                   ▼                          │ Consolidado    │
                          ┌──────────────────┐               │ (read model)   │
                          │   Consolidado    │               └────────────────┘
                          │   Read Model     │
                          └──────────────────┘
```

- **API Lançamentos** (caminho crítico de escrita): persiste o lançamento financeiro e o evento de integração na mesma transação atômica do PostgreSQL (*Transactional Outbox Pattern*). A indisponibilidade do broker ou do consolidado jamais bloqueia a captura de lançamentos.
- **Outbox Publisher**: background service que lê eventos pendentes e publica no RabbitMQ com garantia de entrega *at-least-once*.
- **Worker Consolidado**: processa mensagens da fila com confirmação manual (*ack*), deduplicação por `EventId` para garantia de processamento *exactly-once* sem efeitos colaterais e encaminhamento para DLQ após retentativas.
- **API Consolidado**: serviço de consulta dedicado (*CQRS - Read Model*), atendendo consultas de saldos com latência submilisegundo e isolamento total da base de escrita.

---

## 🎯 Matriz de Requisitos e Capacidades Arquiteturais

| Capacidade / Requisito | Documento de Referência | Entregável no Repositório |
|---|---|---|
| Requisitos Funcionais e Não-Funcionais | [Doc 03](docs/03-requisitos.md) | RFs com critérios de aceitação; RNFs com metas numéricas (SLO) |
| Arquitetura Corporativa: contextos, processos, capacidades | [Doc 02](docs/02-dominios-e-capacidades.md) | Mapa de capacidades, value chain, bounded contexts, responsabilidades |
| Escalabilidade (horizontal, balanceamento, cache) | [Doc 06](docs/06-escalabilidade-resiliencia.md) | Dimensionamento para carga de pico com folga e estratégia elástica |
| Resiliência (redundância, failover, monitoramento, recuperação) | [Doc 06](docs/06-escalabilidade-resiliencia.md) | Defesa em 4 camadas; tolerância a falhas; RTO e RPO |
| Segurança (autenticação, autorização, criptografia, ameaças) | [Doc 07](docs/07-seguranca.md) | Camadas de proteção: borda, rede, transporte, dados e mensageria |
| Padrões arquiteturais (micro/monolito/SOA/serverless) | [ADR-001](docs/adr/ADR-001-estilo-arquitetural.md) | Comparativo formal de alternativas e justificativa técnica |
| Integração (protocolos, formatos, ferramentas) | [Doc 05](docs/05-integracao.md) + [ADR-002](docs/adr/ADR-002-broker.md) | REST/JSON + AMQP/JSON; contrato de eventos versionado |
| Documentação e Representação | Raiz + `docs/` + `c4/` | 100% versionada em Git (Markdown, HTML interativo, PDF) |
| **Evolução:** Arquitetura de Transição (migração de legado) | [Doc 09](docs/09-arquitetura-de-transicao.md) | Estratégia Strangler Fig em 4 fases com paridade e reconciliação |
| **FinOps:** Estimativa de custos de infraestrutura | [Doc 08 §Custos](docs/08-observabilidade-custos.md) | Projeção mensal gerenciada vs minimalista, zero custo de licença |
| **Operação:** Monitoramento e Observabilidade | [Doc 08](docs/08-observabilidade-custos.md) | Métricas-chave, SLI/SLO, tracing distribuído, alertas |
| **Segurança:** Critérios de segurança para consumo de serviços | [Doc 07 §Consumo](docs/07-seguranca.md) + [ADR-007](docs/adr/ADR-007-seguranca-consumo.md) | mTLS, JWT, validação de payload, rate limiting por tenant |

---

## 🚀 Como rodar a solução localmente

### Pré-requisitos
- Docker e Docker Compose instalados.

### Subir o ambiente completo

```bash
docker compose up -d --build
```

O compose inicializa os seguintes serviços:

| Serviço | Endpoint / Porta | Descrição |
|---|---|---|
| **API Lançamentos** | `http://localhost:8080` | Registro de créditos/débitos (Swagger UI na raiz ou `/swagger`) |
| **API Consolidado** | `http://localhost:8081` | Consulta de saldo diário consolidado (Swagger UI na raiz ou `/swagger`) |
| **RabbitMQ Management** | `http://localhost:15672` | Painel do broker (`guest` / `guest`) |
| **PostgreSQL** | `localhost:5432` | Banco relacional (`fluxocaixa` / `fluxocaixa`) |
| **Worker Consolidado** | Background | Consumo de eventos e consolidação assíncrona |
| **DbInit** | Job efêmero | Criação de tabelas e índices na 1ª subida |

---

### Exemplos práticos de uso via cURL

#### 1. Registrar um crédito (com chave de idempotência)

```bash
curl -X POST http://localhost:8080/api/lancamentos \
  -H "Content-Type: application/json" \
  -d '{
        "comercianteId": "11111111-1111-1111-1111-111111111111",
        "data": "2026-11-25",
        "tipo": "credito",
        "valor": 1500.00,
        "descricao": "Venda via cartão de crédito",
        "idempotencyKey": "venda-001-abc"
      }'
```

*Resposta (201 Created):*
```json
{
  "id": "e6a0d24f-9e6e-4c74-a698-c9233634125b",
  "comercianteId": "11111111-1111-1111-1111-111111111111",
  "data": "2026-11-25",
  "tipo": "credito",
  "valor": 1500.00,
  "criadoEmUtc": "2026-11-25T18:00:00Z",
  "jaExistia": false
}
```

#### 2. Registrar um débito

```bash
curl -X POST http://localhost:8080/api/lancamentos \
  -H "Content-Type: application/json" \
  -d '{
        "comercianteId": "11111111-1111-1111-1111-111111111111",
        "data": "2026-11-25",
        "tipo": "debito",
        "valor": 350.00,
        "descricao": "Pagamento de fornecedor",
        "idempotencyKey": "pagto-001-xyz"
      }'
```

#### 3. Consultar o saldo diário consolidado

```bash
curl "http://localhost:8081/api/consolidado/11111111-1111-1111-1111-111111111111?inicio=2026-11-25&fim=2026-11-25"
```

*Resposta:*
```json
{
  "comercianteId": "11111111-1111-1111-1111-111111111111",
  "dias": [
    {
      "data": "2026-11-25",
      "totalCreditos": 1500.00,
      "totalDebitos": 350.00,
      "saldo": 1150.00,
      "quantidadeLancamentos": 2
    }
  ],
  "saldoTotalPeriodo": 1150.00
}
```

#### 4. Listar lançamentos detalhados (com paginação)

```bash
curl "http://localhost:8080/api/lancamentos?comercianteId=11111111-1111-1111-1111-111111111111&inicio=2026-11-01&fim=2026-11-30&pagina=1&tamanhoPagina=20"
```

---

## 🧪 Execução dos testes

```bash
# Testes Unitários de Domínio e Casos de Uso (rápidos, sem dependências externas)
dotnet test tests/Tests.Unit

# Teste de Integração Ponta a Ponta (sobe PostgreSQL e RabbitMQ via Testcontainers)
dotnet test tests/Tests.Integration
```

Total: **14 testes (13 unitários + 1 E2E) passando com 100% de sucesso**.

---

## 📝 Notas de Implementação do Protótipo de Referência

1. **Autenticação e Autorização**: O modelo arquitetural em produção adota autenticação centralizada via OAuth2/OIDC (JWT assinado por Authorization Server) e mTLS no tráfego de mensageria (ver [Doc 07](docs/07-seguranca.md)). No protótipo local via Docker Compose, as rotas foram expostas sem barreira de token para facilitar a validação funcional imediata sem dependência de um IdP externo (Keycloak/Entra ID).
2. **Schema Management**: A inicialização do banco utiliza o container efêmero `DbInit` (`EnsureCreated`). Em ambiente corporativo de produção, essa etapa é substituída por pipelines de migração controladas (*EF Core Migrations* ou *Flyway*).
3. **Observabilidade**: O código-fonte incorpora logging estruturado e correlation ID nas mensagens. Os exportadores do OpenTelemetry para endpoints OTLP remotos são ativados via variáveis de ambiente nas esteiras de observabilidade.

---

## 🏗 Estrutura do projeto

```
├── c4/
│   ├── arquitetura-c4.html        # Visualizador interativo dos diagramas C4
│   └── arquitetura-c4.pdf         # Diagramas formatados em A4 paisagem
├── docs/
│   ├── 01-visao-geral.md          # Visão geral, premissas e método TOGAF
│   ├── 02-dominios-e-capacidades.md # Domínios, mapa de contexto e capacidades
│   ├── 03-requisitos.md           # Requisitos funcionais e SLOs não-funcionais
│   ├── 04-arquitetura-alvo.md     # Desenho de arquitetura alvo detalhado
│   ├── 05-integracao.md           # Protocolos, AMQP, REST e contratos
│   ├── 06-escalabilidade-resiliencia.md # Estratégias de escala e tolerância a falhas
│   ├── 07-seguranca.md            # Modelo de segurança e critérios de consumo
│   ├── 08-observabilidade-custos.md # Observabilidade, SLI/SLO e FinOps
│   ├── 09-arquitetura-de-transicao.md # Estratégia de migração (Strangler Fig)
│   └── adr/                       # 8 Architecture Decision Records formais
├── src/
│   ├── Domain/                    # Entidades, invariantes e eventos de integração
│   ├── Application/               # Use cases, interfaces (portas) e validadores
│   ├── Infrastructure/            # Implementação EF Core, RabbitMQ, Outbox, Consumer
│   ├── Lancamentos.Api/           # API de registro e listagem de lançamentos
│   ├── Consolidado.Api/           # API de leitura de saldos consolidados
│   ├── Consolidado.Worker/        # Worker de consumo assíncrono e consolidação
│   └── DbInit/                    # Inicialização e garantia de schema do PostgreSQL
├── tests/
│   ├── Tests.Unit/                # Testes unitários (Domain, Handlers, Invariantes)
│   └── Tests.Integration/         # Teste E2E (Testcontainers Postgres + RabbitMQ)
├── docker-compose.yml             # Orquestração local dos 5 componentes
└── FluxoCaixa.sln                 # Solução .NET 8
```

---

## 🛑 Parar o ambiente

```bash
docker compose down
# Ou, para remover volumes com dados persistidos:
docker compose down -v
```
