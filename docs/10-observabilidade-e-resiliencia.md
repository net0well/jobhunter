# 10 — Observabilidade e resiliência

## 1. Logs (Serilog)

- Bootstrap em dois estágios (logger de inicialização + configuração completa via `appsettings`).
- Sinks: Console (Development) e File rotativo diário em `logs/` (fora do git), retenção 14 dias.
- Enrichers: `FromLogContext`, `WithMachineName`, `ConnectorKey`, `ApplicationId`, `JobPostingId`, `ScanRunId` via `LogContext.PushProperty` nos handlers.
- Templates estruturados sempre: `logger.LogInformation("Scan of {SourceId} on {Connector} found {Found} postings", ...)`.
- **PII**: destructuring policy que substitui `CandidateProfile`, `FieldValue` e `ApplicationSubmission` por `"***"`. Nunca logar URL de currículo, e-mail ou telefone. Teste automatizado verifica isso.

Níveis:

| Evento | Nível |
|---|---|
| Início/fim de ciclo de worker com contagens | Information |
| Cada chamada a conector | Debug |
| `ConnectorError` esperado (`PostingClosed`, `NotFound`) | Information |
| `LayoutChanged`, `CaptchaDetected`, `AuthenticationRequired`, circuito aberto | Warning |
| Exceção não tratada | Error |

## 2. Traces e métricas (OpenTelemetry)

- `ActivitySource("JobHunter")` com spans: `scan.source`, `posting.upsert`, `application.prepare`, `application.submit`, `connector.call` (atributos `connector.key`, `connector.operation`, `connector.error_kind`).
- Instrumentação: ASP.NET Core, HttpClient, Npgsql.
- Exportação OTLP opcional (`OpenTelemetry:OtlpEndpoint`). Para visualizar localmente, o Aspire Dashboard standalone em Docker (ver `docs/11-infra-local.md`).

Métricas (`IMeterFactory`, meter `JobHunter`):

| Métrica | Tipo | Tags |
|---|---|---|
| `jobhunter.connector.calls` | Counter | `connector`, `operation`, `outcome` |
| `jobhunter.connector.duration` | Histogram (ms) | `connector`, `operation` |
| `jobhunter.postings.discovered` | Counter | `connector` |
| `jobhunter.postings.matched` | Counter | |
| `jobhunter.applications.status_changes` | Counter | `from`, `to`, `channel` |
| `jobhunter.applications.pending_review` | ObservableGauge | |
| `jobhunter.connector.paused` | ObservableGauge | `connector` |

## 3. Health checks

| Endpoint | Checks |
|---|---|
| `/health` | Liveness (processo de pé) |
| `/health/ready` | PostgreSQL (`AspNetCore.HealthChecks.NpgSql`); `BrowserHealthCheck` (somente se algum conector habilitado tem `UsesBrowser`); `ConnectorsHealthCheck` (Degraded se algum conector pausado ou com circuito aberto, com detalhes por conector) |

## 4. Resiliência

### Conectores
Pipelines Polly v8 por conector (`connector:{key}:Read` e `connector:{key}:Submit`), descritos em `docs/06-workers-e-pipeline.md` §6. Registrados dinamicamente para cada conector habilitado com `services.AddResiliencePipeline<string, ConnectorResult<T>>(...)` — use um pipeline não genérico com `ShouldHandle` baseado em `Outcome.Result` quando necessário.

### HTTP (conectores de API, LLM, notificações)
`IHttpClientFactory` com typed clients e `AddStandardResilienceHandler()`; para o LLM, timeout total configurável e **sem** retry em 4xx.

### Navegador
- `BrowserProvider` reinicia o Chromium se ele cair (`Browser.Disconnected`).
- Cada operação tem timeout próprio (`NavigationTimeout`, `ConfirmationTimeout`).
- Falha em qualquer etapa → `ArtifactWriter` salva screenshot + HTML antes de descartar o contexto.

### Banco
`EnableRetryOnFailure` do Npgsql para erros transitórios. Transações explícitas apenas onde há várias escritas que precisam ser atômicas (ex.: criação de `Application` + campos + mudança de status da vaga, que já ocorre num único `SaveChanges`).

## 5. Notificações

`INotifier` com implementação inicial `LogNotifier`. Eventos que notificam:
- candidaturas aguardando revisão (agregado por ciclo);
- conector pausado (captcha, layout, autenticação);
- envio que exige ação manual;
- resumo diário (vagas novas, compatíveis, enviadas).

Telegram ou e-mail entram depois como novas implementações de `INotifier`, sem mudar quem notifica.
