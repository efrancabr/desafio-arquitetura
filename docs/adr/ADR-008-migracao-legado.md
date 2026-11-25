# ADR-008 — Migração de legado: Strangler Fig com espelho reconciliável

**Status:** Aceito

## Contexto

Requisito de rollout: arquitetura de transição considerando convivência e migração de sistema legado.
Premissa assumida: o comerciante opera hoje um sistema legado de controle de caixa; a nova plataforma
não pode interromper a operação nem perder ou duplicar lançamentos.

## Decisão

**Strangler Fig em 4 fases** (espelho → leitura → escrita → desativação), com três técnicas que
apliquei em migrações reais de outros setores:

| Técnica | Origem (experiência real) | Papel na transição |
|---|---|---|
| Camada de integração / anti-corruption | **GBIP** (Banco Inter): apps plugavam indiretamente ao core bancário enquanto o middleware matava o monolito | o legado não se acopla à nova solução; a camada traduz o contrato antigo em `LancamentoCriado` |
| Estrangulamento com paridade e substituição transparente | **Autoban** (Banco BMG): sistema transacional plugado ao Conta Corrente da Matera estrangulando legados | runner espelha o legado; nova plataforma cresce função a função |
| Leitura primeiro, escrita por último | migração **Clipper → .NET** (Athos Pharma) | o consolidado (projeção recalculável, risco baixo) migra antes da escrita (primária, irreversível) |

## Por que essa ordem

Do menor para o maior risco: projeção é recalculável; escrita não. Critérios de saída numéricos por fase
(7 dias de paridade 0 antes de migrar leitura; 30 dias sem escrita antes de desativar). Rollback de banco
de baixo custo em cada fase porque o legado segue operando até a última migração.

## Consequências

- A arquitetura alvo já nasce pronta para a transição: o contrato de evento aceita qualquer produtor; a idempotência dupla torna a dupla gravação reversível; o consolidado é reconstruível por replay.
- Custo: executar reconciliação diária automatizada durante toda a transição — é o preço de migrar sem parar o caixa.
