# ADR-006 — Linguagem e organização do código: .NET 8 + Clean Architecture pragmática

**Status:** Aceito

## Contexto

A escolha tecnológica da solução prioriza confiabilidade para sustentar API transacional de alta vazão,
workers em background resilientes, ORM maduro e testes automatizados determinísticos, evidenciando
a decomposição limpa em domínios de negócio.

## Decisão

**.NET 8 (LTS) / C#** com Clean Architecture pragmática: `Domain` → `Application` → `Infrastructure` →
hosts (APIs + Worker). Regra de dependência estrita; **sem** mediadores genéricos, repositórios genéricos
ou mappers especulativos — abstração onde há troca real de implementação (persistência, broker), tipo
direto onde não há.

| Camada | Contém | NÃO contém |
|---|---|---|
| Domain | entidades com invariantes, eventos, enums | qualquer dependência externa |
| Application | use cases, portas, validação | EF, RabbitMQ, HTTP |
| Infrastructure | EF Core, RabbitMQ, outbox, consumer | regras de negócio |
| Hosts | controllers, composição de DI | lógica |

## Racional

- Ampla maturidade em C#/.NET em sistemas financeiros de missão crítica (Autoban no BMG, migrações na Athos, ERP Refiate), minimizando riscos operacionais e garantindo padrões de engenharia sólidos.
- LTS vigente; ecossistema completo para o problema: `BackgroundService`, EF Core + Npgsql, RabbitMQ.Client oficial, Testcontainers.
- A Clean Architecture pragmática reflete com clareza a fronteira entre domínios no código, garantindo manutenibilidade e desacoplamento efetivo.

## Consequências

- Domínio testável sem infraestrutura (testes unit em milissegundos).
- Mais projetos que um single-app — trade-off consciente: a fronteira física entre domínios precisa existir no código, não só no deploy (ADR-001).
