# 01 — Visão Geral e Método

## O problema de negócio

Um **comerciante** precisa controlar o fluxo de caixa diário: registrar lançamentos (débitos e
créditos) e consultar um **relatório de saldo consolidado por dia**. Duas capacidades de negócio
— controle de lançamentos e consolidação — com exigências arquiteturais diferentes: o primeiro é
transacional e crítico; o segundo é analítico e tolerante a atraso.

## O método aplicado

| Etapa | Referência | Resultado |
|---|---|---|
| 1. Contextualizar o negócio | Cadeia de valor | Entender o comerciante como cliente e o caixa como processo |
| 2. Mapear capacidades | Business Capability Map | Duas capacidades, dois domínios funcionais |
| 3. Refinar requisitos | Levantamento + medição | RFs com aceitação; RNFs com SLO numérico |
| 4. Decompor domínios | DDD (bounded contexts) | Contratos de integração entre contextos |
| 5. Definir arquitetura alvo | C4 + ADRs | Representação em 4 níveis, decisões justificadas |
| 6. Projetar qualidades | SLO-driven | Escalabilidade, resiliência, segurança, custo |
| 7. Planejar transição | Strangler Fig | Migração de legado com rollback por fase |

## Princípios de decisão (o que orienta cada escolha)

1. **Requisito manda, tecnologia obedece.** Cada decisão parte de requisitos reais de negócio — nunca de preferência por ferramenta.
2. **Simplicidade suficiente.** Para 50 rps com um consumidor, a solução é o menor sistema que atende os SLOs com folga mensurável — não o maior que a imaginação permite.
3. **Perda inaceitável, atraso tolerável.** Em sistemas financeiros, dados podem atrasar; não podem desaparecer. Arquitetei para converter falhas em atraso (consistência eventual + compensação), nunca em perda silenciosa.
4. **Trade-off explícito, nunca escondido.** Toda decisão registra o que foi pago por ela (ADRs).
5. **Reusabilidade e flexibilidade.** Contratos de eventos como único acoplamento entre domínios; portas isolando infraestrutura — a mesma arquitetura aceita troca de broker, de banco e novos consumidores sem retrabalho.
6. **Custo como requisito.** Alta disponibilidade vale o preço só no critical path (lançamentos). O consolidado pode cair — por design.
