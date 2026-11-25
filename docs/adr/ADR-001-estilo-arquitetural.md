# ADR-001 — Estilo arquitetural: microsserviços event-driven (2 serviços)

**Status:** Aceito · **Decisor:** Arquiteto de Soluções

## Contexto

O escopo de negócio define dois serviços principais e um requisito de isolamento estrito: o lançamento não pode ficar indisponível
se o consolidado cair. Avaliamos a viabilidade técnica entre monolito, microsserviços, SOA e serverless.

## Alternativas avaliadas

| Estilo | A favor | Contra | Veredito |
|---|---|---|---|
| Monolito (mesmo modular) | 1 deploy, menor custo | compartilha processo: queda do consolidado derruba o registro; **viola RNF-01 por construção** | Descartado |
| Microsserviços síncronos | isolados | chamada do registro para o consolidado reintroduz a dependência proibida (timeout/erro em cascata) | Descartado |
| SOA com ESB | padrão maduro | barramento central = single point of failure e acoplamento de contratos; custo de governança desproporcional | Descartado |
| Serverless | elástico | cold start e orquestração de retry viram código custom a 50 rps; complexidade sem requisito | Descartado |
| **Microsserviços event-driven** | isolamento físico + comunicação assíncrona | custo operacional de broker + consistência eventual | **Decidido** |

## Decisão

Dois serviços fisicamente separados — API Lançamentos (crítico, síncrono na entrada) e o lado
Consolidado (consumer + API read-only) — comunicando-se **apenas por eventos** de domínio no broker.
Nenhuma chamada síncrona cruzada.

## Consequências

- **Positivas:** RNF-01 por construção; escala/deploy independentes; fronteiras de domínio explícitas.
- **Negativas (assumidas):** dois deploys e monitoração de fila; consistência eventual de segundos no saldo — aceitável porque o requisito é **diário**.
- **Volta atrás:** reunificar em monolito modular custaria apenas composição (as portas da Application não mudam).
