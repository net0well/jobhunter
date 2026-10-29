# ADR-008: Fonte de captura separada do canal de candidatura, com vaga canônica

## Status

Proposed

## Context

Com várias plataformas, a mesma vaga pode aparecer em mais de um lugar (ex.: LinkedIn e a página InHire da empresa), e o lugar onde a vaga foi encontrada nem sempre é onde a candidatura acontece (vagas do LinkedIn frequentemente redirecionam para o ATS da empresa).

## Decision

- `JobPosting` é a **vaga canônica**; `PostingSource` registra cada aparição (`connector_key`, `external_id`), com índice único por par.
- Deduplicação em ordem: id externo → URL de candidatura normalizada → fingerprint (empresa, título, cidade) numa janela de 30 dias (`Probable`).
- `JobPosting.apply_channel_key` é resolvido pelo `ApplyChannelResolver`, perguntando a cada `IApplicationChannel` registrado se `CanHandle(applyUrl)`. Sem canal → `ManualApplyOnly`.
- `Application` é única por vaga canônica.

## Consequences

### Positive

- Nunca duas candidaturas para a mesma vaga, mesmo vindo de fontes diferentes.
- Captura e envio podem usar conectores diferentes (ex.: capturar por alerta de e-mail e enviar pelo InHire).

### Negative

- Fingerprint pode gerar falso positivo/negativo.

### Mitigations

- Fingerprint marcado como `Probable` e visível na API; possibilidade futura de "separar vagas" manualmente.
- Normalização coberta por testes de tabela.
