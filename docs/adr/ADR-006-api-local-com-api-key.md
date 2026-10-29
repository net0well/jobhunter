# ADR-006: API somente em localhost com API key

## Status

Proposed

## Context

A API serve apenas ao próprio usuário, na própria máquina. As regras de segurança do projeto exigem autenticação explícita em todo endpoint. JWT/OIDC seria excessivo agora.

## Decision

- Kestrel escuta apenas em `localhost`.
- Grupo `/api` protegido por `ApiKeyEndpointFilter` (cabeçalho `X-Api-Key`, comparação em tempo constante, chave em user-secrets).
- `/health` e `/scalar` marcados explicitamente como anônimos; Scalar só em Development.
- Sem CORS (sem front-end de outra origem por enquanto).

## Consequences

### Positive

- Simples, seguro para uso local, compatível com Scalar (prefill da chave).

### Negative

- Não serve para acesso remoto ou multiusuário.

### Mitigations

- Se a aplicação for exposta, migrar para OIDC antes, com ADR próprio.
