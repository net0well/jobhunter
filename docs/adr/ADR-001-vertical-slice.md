# ADR-001: Vertical Slice Architecture no núcleo

## Status

Proposed

## Context

Projeto pessoal, um desenvolvedor, ~10 entidades, uma máquina de estados principal (candidatura) e regras de pontuação simples. A complexidade real está nas integrações com plataformas externas, não no domínio.

Alternativas avaliadas:
- **Clean Architecture (4 projetos)**: separação forte, mas 5–6 arquivos por operação para um domínio pequeno. Custo alto sem benefício proporcional.
- **DDD + CA**: não há invariantes complexas entre agregados nem linguagem de domínio especializada.
- **Modular Monolith**: não há múltiplos bounded contexts nem times.
- **VSA**: uma operação por arquivo, alinhado a minimal APIs.

## Decision

Usar VSA no projeto `JobHunter.Api`: `Features/<Área>/<Operação>.cs` com Command/Query, Validator, Handler e Response; endpoints por área com `IEndpointGroup`; handlers com Mediator (source generator, MIT).

Entidades com comportamento ficam em `Domain/` dentro do mesmo projeto, porque são compartilhadas por várias features. Serviços puros compartilhados dentro de uma área ficam em `Features/<Área>/Shared/`; entre áreas, em `Common/`.

A variabilidade das plataformas é tratada à parte (ADR-007).

## Consequences

### Positive

- Entrega rápida de features; poucas pastas a navegar.

### Negative

- Sem barreira de compilação entre "domínio" e "infra" dentro do host.

### Mitigations

- Regra de não usar EF Core dentro de `Domain/` verificada em revisão (`/code-review`) e por teste de arquitetura.
- Evolução para Clean Architecture documentada em `docs/02-arquitetura.md` §9 se o domínio crescer.
