# 05 — API de revisão

Minimal API local, documentada com OpenAPI nativo e Scalar (`/scalar`).

## 1. Convenções

- Prefixo `/api`. Sem versionamento por enquanto (uso local, um único cliente).
- Um `IEndpointGroup` por área em `Features/<Área>/<Área>Endpoints.cs`; `Program.cs` só chama `app.MapEndpoints()`.
- `TypedResults` + `.WithName()`, `.WithSummary()`, `.Produces...()` em todo endpoint.
- Validação com FluentValidation via `ValidationFilter<T>`.
- Erros sempre em ProblemDetails (RFC 9457) via `Result.ToProblemDetails()`; `GlobalExceptionHandler` para o inesperado.
- Paginação: `?page=1&pageSize=20` (máx. 100) → `PagedResponse<T>(items, page, pageSize, totalCount)`.
- Autenticação: API key no cabeçalho `X-Api-Key` (ADR-006). Todos os endpoints de `/api` usam `.AddEndpointFilter<ApiKeyEndpointFilter>()` no grupo; `/health` e `/scalar` explicitamente anônimos e só em `localhost`.
- Datas em ISO 8601 UTC.

### Mapeamento de erros

| `ErrorType` | HTTP |
|---|---|
| `Validation` | 400 |
| `Unauthorized` | 401 |
| `NotFound` | 404 |
| `Conflict` | 409 |
| `Unprocessable` | 422 |
| `Unavailable` (ex.: conector pausado) | 503 |
| `Unexpected` | 500 |

```csharp
public enum ErrorType { Validation, Unauthorized, NotFound, Conflict, Unprocessable, Unavailable, Unexpected }

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable);
    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);
}
```

O `code` vai no ProblemDetails em `extensions.code` para a UI poder reagir.

## 2. Endpoints

### Conectores

| Verbo | Rota | Descrição | Resposta |
|---|---|---|---|
| GET | `/api/connectors` | Conectores habilitados com capacidades, estado (pausado, último erro) e envios do dia | `ConnectorResponse[]` |

### Fontes monitoradas

| Verbo | Rota | Descrição | Erros |
|---|---|---|---|
| GET | `/api/sources?connector=&active=` | Lista fontes | |
| GET | `/api/sources/{id}` | Detalhe com últimas execuções | 404 |
| POST | `/api/sources` | Cria fonte. Body: `{ connectorKey, kind, input?, searchCriteria?, displayName?, scanInterval? }`. `Tenant` exige `input` (URL ou id; o conector faz `ParseTenant`); `Search` exige `searchCriteria: { keywords, location?, workModels?, maxResults? }`; `Feed` não tem input (um por conector). `scanInterval` entre 5 min e 7 dias | 400 `Source.InvalidInput`; 409 `Source.Duplicate`; 409 `Connector.CapabilityMissing`; 404 `Connector.NotFound` |
| POST | `/api/sources/import` | Importa várias: `{ connectorKey, inputs: string[] }`. Retorna criadas, duplicadas e inválidas | 400 |
| PUT | `/api/sources/{id}` | Atualiza nome, intervalo, ativo | 404 |
| DELETE | `/api/sources/{id}` | Remove a fonte (vagas permanecem) | 404 |
| POST | `/api/sources/{id}/scan` | Enfileira varredura imediata; 202 com `scanRunId` (`ScanRun` nasce `Running`) | 404; 503 `Connector.Paused`; 503 `Connector.Disabled`; 503 `Scan.QueueFull` |

### Varreduras

| Verbo | Rota | Descrição |
|---|---|---|
| GET | `/api/scan-runs?sourceId=&connector=&status=&page=` | Histórico |

### Vagas

| Verbo | Rota | Descrição | Erros |
|---|---|---|---|
| GET | `/api/postings?status=&connector=&minScore=&q=&page=` | Lista paginada, ordenada por `match_score desc, first_seen_at desc` | |
| GET | `/api/postings/{id}` | Detalhe com fontes, motivos do score, canal de candidatura | 404 |
| POST | `/api/postings/import` | `{ url, title?, companyName? }` — importa vaga por URL: usa o `IJobDetailsProvider` que aceitar a URL; senão cria registro mínimo com `connector_key = "manual"` (exige `title`; empresa vem de `companyName` ou do host) | 400 `Posting.TitleRequired`; 409 `Posting.AlreadyExists` |
| POST | `/api/postings/{id}/discard` | `{ reason }` — descarte manual, preservado pelo recálculo | 404; 409 |
| POST | `/api/postings/{id}/restore` | Volta para `Matched` | 404; 409 |
| POST | `/api/postings/{id}/refresh` | Atualiza detalhes pelo conector | 404; 409 `Connector.CapabilityMissing` |
| POST | `/api/postings/{id}/apply` | Cria a candidatura agora (dispara `PrepareApplication`) | 404; 409 `Application.AlreadyExists`; 422 `Posting.ManualApplyOnly` |

### Empresas

| Verbo | Rota | Descrição | Erros |
|---|---|---|---|
| GET | `/api/companies?blocked=&q=` | Empresas vistas nas vagas, com contagem de vagas | |
| POST | `/api/companies/{id}/block` | Bloqueia; as vagas abertas voltam para `New` e a pontuação as descarta (`CompanyBlocked`) | 404 |
| POST | `/api/companies/{id}/unblock` | Desbloqueia; as vagas descartadas pelo bloqueio voltam para a pontuação | 404 |

### Perfis de busca

| Verbo | Rota | Descrição |
|---|---|---|
| GET | `/api/search-profiles` | Lista |
| POST | `/api/search-profiles` | Cria |
| PUT | `/api/search-profiles/{id}` | Atualiza |
| DELETE | `/api/search-profiles/{id}` | Remove |
| POST | `/api/search-profiles/rescore` | Recalcula notas de vagas `New`/`Matched`/`Discarded` (não mexe em vagas com candidatura) |

Exemplo de primeiro perfil (`POST /api/search-profiles`; campos omitidos ficam vazios, `minScore` padrão 60 e pesos padrão `title 35, description 25, location 15, workModel 15, seniority 10, descriptionPenalty 10`):

```json
{
  "name": "Backend .NET",
  "requiredKeywords": [".net", "c#"],
  "preferredKeywords": ["asp.net core", "postgresql", "docker", "azure", "microsserviços"],
  "excludedKeywords": ["estágio", "estagiário", "trainee"],
  "excludedDescriptionKeywords": ["plantão", "sobreaviso"],
  "locations": ["São Paulo", "Campinas"],
  "workModels": ["Remote", "Hybrid"],
  "seniorities": ["pleno", "sênior"],
  "minScore": 60
}
```

Regras sem dado (vaga sem local, perfil sem senioridades...) ficam fora da nota, que é normalizada sobre os pesos aplicáveis. Alterar um perfil não recalcula sozinho: use `rescore`.

### Candidaturas

| Verbo | Rota | Descrição | Erros |
|---|---|---|---|
| GET | `/api/applications?status=&page=` | Lista; padrão `status=PendingReview` | |
| GET | `/api/applications/{id}` | Detalhe com vaga, campos, respostas propostas e finais, origem da proposta, evidências | 404 |
| PUT | `/api/applications/{id}/answers` | `{ answers: [{ key, text?, selectedOptions?, filePath? , saveAsTemplate? }] }` | 404; 409 `Application.InvalidStatus`; 400 `Answer.InvalidOption`/`Answer.TooLong` |
| POST | `/api/applications/{id}/approve` | Aprova | 404; 409 `Application.InvalidStatus`; 422 `Application.UnansweredRequired`; 422 `Application.ConsentNotReviewed` |
| POST | `/api/applications/{id}/skip` | `{ reason }` | 404; 409 |
| POST | `/api/applications/{id}/submit-now` | Envia imediatamente (só `Approved`) | 404; 409; 409 `Application.DailyLimitReached`; 503 `Connector.Paused` |
| POST | `/api/applications/{id}/open-assisted` | Abre navegador visível preenchido (se o canal suporta) | 404; 422 `Connector.AssistedUnsupported` |
| POST | `/api/applications/{id}/retry` | `Failed` → `PendingReview` com formulário reinspecionado | 404; 409 |
| POST | `/api/applications/{id}/mark-submitted` | Você enviou manualmente (`RequiresManualAction` → `Submitted`) | 404; 409 |
| GET | `/api/applications/{id}/evidence/{index}` | Baixa screenshot/HTML de evidência (somente localhost) | 404 |

Exemplo de `GET /api/applications/{id}`:

```json
{
  "id": "0199f0c2-...",
  "status": "PendingReview",
  "channel": "inhire",
  "posting": {
    "id": "0199f0b1-...",
    "title": "Desenvolvedor .NET Pleno",
    "company": "Empresa X",
    "location": "Remoto",
    "matchScore": 82,
    "applyUrl": "https://empresax.inhire.app/vagas/..."
  },
  "fields": [
    {
      "key": "email",
      "label": "E-mail",
      "kind": "Email",
      "semantic": "Email",
      "required": true,
      "proposed": { "text": "<do perfil>" },
      "proposalSource": "Profile",
      "final": { "text": "<do perfil>" },
      "reviewed": true
    },
    {
      "key": "q_123",
      "label": "Você tem experiência com Azure?",
      "kind": "SingleChoice",
      "semantic": "Custom",
      "required": true,
      "options": [ { "value": "1", "label": "Sim" }, { "value": "2", "label": "Não" } ],
      "proposed": { "selectedOptions": ["1"] },
      "proposalSource": "Template",
      "final": null,
      "reviewed": false
    }
  ],
  "pendingRequired": 1,
  "evidence": []
}
```

### Banco de respostas

| Verbo | Rota | Descrição |
|---|---|---|
| GET | `/api/answer-templates?q=` | Lista/busca |
| POST | `/api/answer-templates` | Cria |
| PUT | `/api/answer-templates/{id}` | Atualiza |
| DELETE | `/api/answer-templates/{id}` | Desativa |

### Perfil do candidato

| Verbo | Rota | Descrição |
|---|---|---|
| GET | `/api/profile` | Perfil (sem mascarar, só localhost) |
| PUT | `/api/profile` | Atualiza |
| POST | `/api/profile/resume` | Upload de PDF (multipart, máx. 5 MB); grava em `Profile:ResumeDirectory`, extrai texto |

### Descoberta

| Verbo | Rota | Descrição |
|---|---|---|
| POST | `/api/discovery/{connectorKey}` | `{ query }` — retorna tenants sugeridos (não cria fontes) |

### Operacional

| Verbo | Rota | Descrição |
|---|---|---|
| GET | `/health` | Liveness |
| GET | `/health/ready` | PostgreSQL, navegador (se algum conector usa), estado dos conectores |
| GET | `/scalar` | UI da API (somente Development) |
| GET | `/openapi/v1.json` | Documento OpenAPI |
