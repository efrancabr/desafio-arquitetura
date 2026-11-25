# ADR-007 — Segurança no consumo de serviços (integração)

**Status:** Aceito

## Contexto

Requisito corporativo: critérios de segurança para consumo e integração de serviços. Os serviços são
consumidos por clientes (PDV/interface) e por outros serviços integradores (consumidores do broker).

## Decisão — regra para todos os consumidores

1. **Identidade antes do acesso:** JWT/OIDC para APIs; credencial de aplicação por vhost para o broker. No protótipo local os endpoints ficam abertos para facilitar testes funcionais; em produção, identidade é pré-requisito de contrato.
2. **Autorização derivada do token, nunca do corpo:** o `comercianteId` vem da claim — o cliente não pode se passar por outro comerciante editando um campo do JSON.
3. **Menor privilégio até no banco:** API de leitura só tem `SELECT`; usuário de banco = serviço.
4. **Integridade de eventos:** consumo via TLS no transporte + HMAC do payload quando houver produtores externos; próprio outbox dispensa HMAC nesta fase (origem confiável) — critério explícito de quando exigir.
5. **Rate limit por consumidor** (429) — o tráfego de um não degrada os demais.
6. **Validação de entrada em tráfego interno também** — evento de fila é dado externo; o consumer valida antes de projetar (nunca confiar por estar "dentro").

## Trade-off registrado

Tokens JWT exigem um Authorization Server (Keycloak / Entra ID). No protótipo local via Docker Compose,
as rotas foram expostas sem barreira de token para viabilizar execução imediata sem dependências externas;
para produção, o desenho de autorização já está posicionado (claim → autorização por contexto) e não
exige mudanças no domínio de negócio.
