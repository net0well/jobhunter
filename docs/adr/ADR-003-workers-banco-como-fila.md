# ADR-003: Workers com o banco como fila, sem message broker

## Status

Proposed

## Context

O pipeline tem etapas assíncronas (varrer, pontuar, preparar, enviar). Opções: message broker (Wolverine/RabbitMQ), Hangfire, ou `BackgroundService` lendo estados do banco. Não vale introduzir mensageria antes de ser necessária.

## Decision

`BackgroundService` por etapa, com `PeriodicTimer` + `TimeProvider`, consultando entidades por status/data e enviando commands via Mediator dentro de um scope. Não há broker nem Hangfire.

## Consequences

### Positive

- Zero infraestrutura extra; tudo visível no banco e pela API.
- Retomável após reinício; fácil de testar chamando os commands.

### Negative

- Polling em vez de reação imediata.
- Supõe instância única.

### Mitigations

- Intervalos curtos de tick e endpoints de disparo manual (`/scan`, `/submit-now`).
- Índices únicos e `xmin` evitam duplicidade; caminho para `FOR UPDATE SKIP LOCKED` ou Wolverine documentado.
