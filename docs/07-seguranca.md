# 07 — Segurança

## Modelo por camada (borda → transporte → dados → mensageria)

### 1. Borda (APIs)

| Controle | Implementação em Produção | Status no Protótipo Local |
|---|---|---|
| Autenticação | OAuth2/OIDC com JWT (Authorization Server) | Aberto localmente para facilitar testes e smoke tests; injetado via middleware em staging/prod |
| Autorização | `comercianteId` derivado do **token**, nunca do body da requisição | Impede um comerciante de ler o caixa de outro (regra crítica de design) |
| Validação de entrada | FluentValidation na borda + invariantes no domínio (2ª barreira) | Implementado no código-fonte |
| Rate limiting | Por credencial (ex.: 100 req/s), com 429 | Configurado em API Gateway / Ingress em produção |
| Transporte | TLS 1.2+ obrigatório; HSTS | Terminação no ingress / reverse proxy |
| Cabeçalhos/OWASP | Content-Security-Policy, X-Content-Type-Options etc. | Via gateway / middleware em produção |

### 2. Dados

- **Em repouso:** criptografia de disco gerenciada (cloud) / LUKS on-prem; `decimal(18,2)` ponta a ponta (nunca `float`).
- **Em trânsito:** TLS em todas as conexões (API↔banco, API↔broker, broker↔worker).
- **Privilégio mínimo por serviço (PostgreSQL):** API Lançamentos `INSERT/SELECT` em `lancamentos`+`outbox`; Worker `INSERT/SELECT` em `saldos`+`processed`; API Consolidado **só `SELECT`**. Cada usuário de banco é um serviço.
- **Nunca logar dados sensíveis:** logs estruturados sem chaves, tokens ou valores completos de PII.
- **Backups criptografados** e com teste de restore (RNF-08).

### 3. Mensageria (segurança do broker)

| Controle | Ação |
|---|---|
| Credenciais | Usuário dedicado por aplicação (nunca `guest` em produção) |
| Transporte | TLS/AMQPS na conexão |
| Integridade do payload | Assinatura HMAC do evento quando houver produtores além do próprio outbox (ver ADR-007) |
| Autorização no broker | Permissões restritas por vhost |

## Critérios de segurança para consumo e integração de serviços

Regras que qualquer consumidor interno/externo dos serviços deve cumprir:

1. **Identidade estabelecida antes do acesso** — JWT válido (API) ou credencial de aplicação (broker).
2. **Menor privilégio** — o consumidor recebe exatamente as permissões do seu use case (leitura de saldo ≠ escrita de lançamento).
3. **Não-repúdio e rastreabilidade** — todo acesso auditável (actor, horário, recurso) via logs/trails.
4. **Rate limit por consumidor** — tráfego de um consumidor não pode degradar os demais.
5. **Integridade dos eventos consumidos** — consumer valida assinatura/integridade quando o produtor não é confiável (HMAC/mTLS).
6. **Validação de entrada mesmo em tráfego interno** — eventos de fila são dado externo; consumer valida antes de projetar.

## Análise de ameaças (STRIDE lite — os que importam para este domínio)

| Ameaça | Vetor | Mitigação |
|---|---|---|
| **S**poofing | Token/credencial falsa | OIDC + validação de assinatura JWT; credenciais por vhost |
| **T**ampering | Alteração de evento na fila | HMAC do payload (ADR-007); TLS ponta a ponta |
| **I**nformation disclosure | Comerciante lendo saldo alheio | Autorização por claim de token (nunca body) |
| **D**enial of Service | Abuso de endpoints | Rate limit; fila como amortecedor (não derruba o crítico) |
| **E**levation | Usuário comum virando admin | Escopos separados; menor privilégio no banco |

## Conformidade e princípios

- Dados financeiros ⇒ tratamento equivalente a LGPD (minimização: o sistema grava apenas o necessário ao fluxo de caixa — nada de PII além do identificador).
- Security by design: idempotência (ADR-005) também é defesa financeira — replay de requisição não duplica valor.
