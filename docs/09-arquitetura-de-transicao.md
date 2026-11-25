# 09 — Arquitetura de Transição (migração de legado)

> **Estratégia de Engenharia** — Planejamento de convivência e migração gradativa de plataforma legada.

## Cenário assumido

O comerciante não nasce com a solução nova: ele opera um **legado de controle de caixa** (sistema
anterior — ERP antigo, planilha estruturada ou módulo próprio de conta corrente que registra os
lançamentos de hoje). A pergunta central da transição é: **como colocar a nova plataforma de
fluxo de caixa em produção sem interromper a operação diária nem perder ou duplicar um único lançamento financeiro?**

> As três técnicas desta seção vêm de migrações reais que conduzi em outros setores (bancário e
> varejo). Transponho-as para o caixa comercial porque o método de estrangulamento depende da
> natureza do risco (leitura derivada vs. escrita primária), não do setor em que o sistema roda.

## Estratégia: Strangler Fig — "estrangulamento" do legado

Princípio: a nova plataforma **cresce em volta** do legado, absorvendo capacidade por capacidade. O
legado só morre quando a última função migra — e até lá, cada fase tem rollback de um comando.

```
FASE 1 (espelho do passado)          FASE 2 (novo assume leitura)        FASE 3 (novo assume a escrita)
┌────────┐  grava   ┌─────────────┐  ┌────────┐ leitura nova ┌─────────┐  ┌────────┐   ┌─────────────┐
│ Legado │ ───────▶ │ Legado      │  │ Legado │ ───────────▶ │ Nova API│  │ Legado │   │ Nova API    │
│ (caixa)│          │ + espelho→  │  │(caixa) │              │  (saldo)│  │(caixa) │   │ (escrita)   │
└────────┘          │   novo banco│  └────────┘              └─────────┘  └────────┘   └─────────────┘
                    └─────────────┘  continua gravando no legado;          read-only p/ auditoria;
                    distribui eventos do legado                           espelho desligado
```

## As três técnicas

### Técnica 1 — Camada de Integração com Anti-Corruption (GBIP, Banco Inter)

> No Banco Inter, projetei o middleware **GBIP**: aplicações plugavam-se **de forma indireta**
> ao core bancário por trás da camada de integração — e, ao mesmo tempo, o middleware concretizava a
> morte de um sistema legado monolítico, migrando o tráfego função por função sem os clientes dos
> contratos perceberem.

**Aplicação aqui:** no fluxo de caixa, a mesma função — o *estrangulador de integração*: um componente
(anti-corruption layer) expõe o contrato novo (`LancamentoCriado`) e traduz o tráfego do legado. Durante
a transição, o legado **não chama a nova API**; a camada absorve o jeito antigo de lançar e emite o
evento novo. Resultado: zero mudança no legado, zero dependência nova no legado.

### Técnica 2 — Runner com espelho de dados e reconciliação (Autoban, Banco BMG)

> No Banco BMG, liderei a criação do **Autoban** — sistema transacional em C#/.NET que
> **estrangulava** legados e operava plugado ao Conta Corrente da Matera, tornando-se a espinha dorsal
> dos sistemas transacionais do banco: harmonizava legado e novo, substituindo fornecedores de forma
> transparente.

**Aplicação aqui:** na Fase 1, cada lançamento continua sendo gravado onde sempre foi — e um **runner**
(after-commit no legado, exportação incremental, ou CDC) espelha para a nova base. A reconciliação
diária (contagem vs. soma por dia) é o **critério de saída** da fase: só se migra leitura quando 7 dias
consecutivos baterem. Substituir sem bater contagem é a diferença entre migração e perda de dinheiro.

### Técnica 3 — Inverter a leitura primeiro, a escrita por último (Clipper → .NET, Athos Pharma)

> Na Athos Pharma, migrei sistemas antigos (Clipper) para .NET: corte de **leitura** antes de
> corte de **escrita** — o novo sistema serve relatórios enquanto o antigo ainda grava.

**Aplicação aqui:** o primeiro valor visível da nova plataforma é o **consolidado** (é projeção —
recalculável, reversível, de baixo risco). Só quando o saldo relatado pela nova solução provar paridade
com a verdade do legado é que a escrita dos lançamentos pode migrar. Nunca o contrário.

## Plano por fases com critérios de saída

| Fase | O que acontece | Critério de saída (objetivo) | Rollback |
|---|---|---|---|
| **0 — Reconciliação estrutural** | inventário do legado: tabelas, formatos, regras de fechamento de dia | mapa de dados aprovado pelo dono do negócio | — |
| **1 — Espelho + runner** | legado grava onde sempre gravou; runner publica eventos; saldo novo calculado **em paralelo** | 7 dias de reconciliação com divergência 0 | desliga runner (nada dependia da nova) |
| **2 — Leitura migrada** | relatório de saldo passa a ser servido pela nova API; legado continua escrevendo | UAT com usuário real; paridade diária mantida | reverter DNS/rota para o relatório antigo |
| **3 — Escrita migrada** | ponto de captura (PDV/interface) aponta para a nova API; legado vira read-only de auditoria | 30 dias sem escrita no legado | redirecionar captura ao legado (os dados continuam lá até a desativação) |
| **4 — Desativação** | legado congelado, dados arquivados, espelho desligado | auditoria concluída, retenção legal definida | restauração do arquivo |

**Por que essa ordem:**
- Leitura é **derivada e recalculável** (risco baixo) — escrita é **primária e irreversível** (risco alto). Migra-se do menor para o maior risco.
- O legado **nunca é modificado** até a Fase 3 — o risco mais comum em migrações (quebrar o que fatura) é zerado.
- Cada fase tem **critério de saída numérico** (dias de paridade, dias sem escrita) — sem "quando estiver pronta".

## O que a arquitetura alvo já nasce preparada para isso

- O contrato `LancamentoCriado` aceita **qualquer produtor** — o runner do legado é só mais um publisher no outbox/exchange. O worker não sabe (nem precisa saber) se o evento nasceu no legado ou no novo.
- A idempotência dupla (chave de negócio + MessageId) é exatamente o mecanismo que torna **dupla gravação reversível**: o espelho pode reenviar sem duplicar valor.
- O contexto Consolidado, sendo projeção, é **reconstruível por replay** (via outbox/fila) — exercício que em produção real é o teste de fogo da transição.

## Riscos assumidos e como são tratados

| Risco | Tratamento |
|---|---|
| Divergência legado × novo | Reconciliação diária automatizada como critério de saída |
| Duplicidade durante espelho | Idempotência por chave de negócio (do legado) no próprio contrato |
| Janela de latência legado→novo | Leitura migra só após paridade; frescor é medido e alertado |
| Retrabalho de integração no legado | Proibido: todo acoplamento novo nasce na camada anti-corruption |
