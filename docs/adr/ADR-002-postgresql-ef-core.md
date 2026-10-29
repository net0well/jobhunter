# ADR-002: PostgreSQL com EF Core, sem repository, nomes em snake_case

## Status

Proposed

## Context

Escolha do usuário: PostgreSQL. EF Core é usado usado diretamente (DbContext já é Unit of Work + Repository) e Testcontainers para testes com o banco real.

## Decision

- `Npgsql.EntityFrameworkCore.PostgreSQL` + `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention()`).
- `AppDbContext` injetado diretamente nos handlers. Sem `IRepository<T>`.
- `uuid` v7 gerado na aplicação, `timestamptz` com `DateTimeOffset`, enums como `text`, `text[]` e `jsonb` para listas e estruturas flexíveis, `xmin` como token de concorrência.
- Migrations no projeto `JobHunter.Api` em `Common/Persistence/Migrations`, aplicadas com `dotnet ef database update` (sem `Migrate()` automático).
- Testes com `Testcontainers.PostgreSql` na mesma versão major do Compose.

## Consequences

### Positive

- Acesso a todos os recursos do EF Core (projeções, `ExecuteUpdateAsync`, compiled queries).
- Banco legível em ferramentas SQL; arrays e jsonb evitam tabelas auxiliares desnecessárias.

### Negative

- Acoplamento ao PostgreSQL (arrays, jsonb, `xmin`).

### Mitigations

- Aceitável: PostgreSQL é decisão do projeto. Consultas específicas ficam nas configurações e handlers, fáceis de localizar.
