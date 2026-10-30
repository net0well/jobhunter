# 06 — Workers e pipeline

Os workers são `BackgroundService` no mesmo processo da API. O banco funciona como fila (ADR-003): cada etapa consulta registros num estado e os move para o próximo. Isso torna o pipeline retomável após reinício e visível pela API.

## 1. Visão geral

| Worker | Intervalo (padrão) | Command enviado | Lê | Produz |
|---|---|---|---|---|
| `SourceScanWorker` | tick 5 min | `ScanDueSources` | `SourceTarget` com `next_scan_at <= now` | `JobPosting`/`PostingSource` (`New`), `ScanRun` |
| `MatchingWorker` | tick 2 min | `ScorePendingPostings` | `JobPosting` `New` | `Matched` ou `Discarded` |
| `PreparationWorker` | tick 5 min | `PreparePendingApplications` | `JobPosting` `Matched` com canal e sem candidatura (se `Applications:AutoPrepare = true`) | `Application` `PendingReview` |
| `SubmissionWorker` | tick 10 min, dentro da janela | `SubmitApprovedApplications` | `Application` `Approved` | `Submitted` / `Failed` / `RequiresManualAction` |
| `MaintenanceWorker` | 1×/hora | `RunMaintenance` | — | zera contadores diários, aplica retenção de `ScanRun` e `Artifacts`, reativa conectores com pausa vencida |

O tick é a frequência com que o worker **verifica** se há trabalho. A frequência real de varredura de cada fonte é `SourceTarget.scan_interval ?? Connectors:<Key>:ScanInterval`.

Todos os workers:
- usam `PeriodicTimer` com `TimeProvider` (testável com `FakeTimeProvider`);
- criam um scope por ciclo (`IServiceScopeFactory`) e só chamam `ISender.Send(...)`;
- respeitam `Workers:<Nome>:Enabled` (desligados nos testes de integração);
- processam lotes limitados (`BatchSize`) por ciclo;
- registram duração e itens processados como métricas.

## 2. Varredura (`ScanDueSources` → `ScanSource`)

```
fontes = SourceTargets ativas e vencidas de conectores habilitados e não pausados,
        ordenadas por next_scan_at, limitadas a BatchSize
agrupar por connector_key
para cada conector (em paralelo até Scanning:MaxParallelConnectors):
    para cada fonte do conector (sequencial, um scope por fonte):
        ScanSource(fonte)
        se o erro foi de bloqueio (layout, captcha, autenticação, rate limit) → parar este conector no ciclo
```

`ScanSource`:
1. Resolve a capacidade conforme `kind`: `ITenantJobSource`, `ISearchJobSource` ou `IFeedJobSource`.
2. Chama via `ConnectorInvoker` (rate limit + resiliência + métricas).
3. Sucesso → `PostingUpserter` com os itens; vagas novas sem descrição têm os detalhes buscados na hora (via `ConnectorInvoker`; falha deixa o `ScanRun` como `PartiallySucceeded`); atualiza `checkpoint` (feed/busca); `RecordSuccess` na fonte e no `ConnectorState`.
4. Para fontes `Tenant`: incrementa `missed_scans` das `PostingSource` daquele conector + fonte que **não** vieram na lista; fecha vagas cujas fontes todas atingiram `ClosedAfterMissedScans`.
5. Falha → `RecordFailure` (desativa a fonte após `Scanning:DeactivateAfterFailures` e notifica); `LayoutChanged`/`CaptchaDetected`/`AuthenticationRequired` pausam o conector por `Scanning:BlockedPauseDuration` (6 h), `RateLimited` com `RetryAfter` pausa pelo tempo pedido, e notificam. Erros gerados pelo próprio núcleo (`Connector.Paused`, rate limit local) não contam como falha da fonte.
6. Grava `ScanRun`.

`PostingUpserter` (`Features/Postings/Shared`) aplica a deduplicação descrita em `docs/02-arquitetura.md` §4.3, cria a `Company` quando não existe, reabre vagas fechadas que reaparecem e resolve o canal com `ApplyChannelResolver`.

`POST /api/sources/{id}/scan` cria o `ScanRun` como `Running` e o coloca numa fila em memória (`ScanRequestQueue`); o `ManualScanWorker` consome a fila e executa o mesmo `ScanSource`.

## 3. Pontuação (`ScorePendingPostings`)

`MatchScorer` é puro (sem I/O) e testado por tabela:

| Regra | Efeito |
|---|---|
| Empresa bloqueada | descarta (`CompanyBlocked`) |
| `excluded_keywords` no título | descarta (`ExcludedKeyword:<termo>`) |
| Nenhuma `required_keywords` em título/descrição | descarta (`MissingRequired`) |
| `required_keywords` ou `preferred_keywords` no título | + peso `title` |
| `preferred_keywords` na descrição | + peso `description` proporcional; 3 termos (ou todos, se forem menos) já valem o peso cheio |
| Local compatível (cidade em `locations` ou `Remote` aceito) | + peso `location`; incompatível e não remoto → descarta |
| Modelo de trabalho aceito | + peso `workModel` |
| Senioridade compatível | + peso `seniority`; ausente = neutro |
| `excluded_description_keywords` | − `descriptionPenalty` por termo, até 30 pontos |

Regras sem dado de um dos lados (vaga sem local ou senioridade, perfil sem critério) ficam fora do cálculo; a nota é a soma dos pesos atendidos sobre a soma dos pesos aplicáveis, 0–100, menos a penalidade. Com vários perfis ativos vale o melhor resultado. Sem perfil ativo, as vagas continuam `New`. `score >= min_score` → `Matched`; abaixo → `Discarded` com `LowScore`. `match_reasons` guarda cada regra aplicada para você entender a nota.

## 4. Preparação (`PrepareApplication`)

1. Garante que não há `Application` para a vaga (índice único também protege).
2. Exige `apply_channel_key`; sem canal → erro `Posting.ManualApplyOnly`.
3. `IApplicationChannel.InspectAsync(applyUrl)` via `ConnectorInvoker`.
4. `FormAnswerer.Answer(form, candidateProfile, templates, posting)`:
   - semântica conhecida → valor do perfil (`proposal_source = Profile`, `reviewed = true` para campos simples como nome/e-mail);
   - `Resume` → `CandidateProfile.resume_file_path`;
   - `ConsentTerms`/`ConsentTalentPool` → **sem** proposta; `reviewed = false` obrigatório;
   - `Custom` → `TemplateAnswerSuggester`; se não houver e LLM habilitado → `AnthropicAnswerSuggester` (`proposal_source = Llm`, `reviewed = false`);
   - escolha única/múltipla: proposta só se a opção existir exatamente no formulário.
5. Cria `Application` (`PendingReview`) + `ApplicationField[]`, `JobPosting.StartApplication()`.
6. Notifica "N candidaturas aguardando revisão" (agregado, não uma por vaga).

## 5. Envio (`SubmitApprovedApplications` → `SubmitApplication`)

Pré-condições checadas **no handler** `SubmitApplication` (não no worker):
- `status == Approved`;
- dentro da janela `Applications:SubmissionWindow` (ex.: 08:00–20:00 no fuso `App:TimeZone`);
- `ConnectorState.submissions_today < Connectors:<Key>:DailySubmissionLimit` e total do dia `< Applications:DailySubmissionLimit`;
- conector não pausado.

Execução:
1. `Application.StartSubmitting(now)` e `SaveChanges` (com `xmin`, evita envio duplo concorrente).
2. `SubmissionBuilder` monta `ApplicationSubmission` com os `final_value`.
3. `IApplicationChannel.SubmitAsync` via `ConnectorInvoker` **sem retry automático** (pipeline `connector:{key}:submit` só tem timeout): reenviar um formulário pode gerar candidatura duplicada.
4. Resultado:
   - sucesso → `MarkSubmitted(receipt)`, incrementa `submissions_today`;
   - `FormChanged` → `ResetForReview(novoFormulário)`;
   - `CaptchaDetected`/`AuthenticationRequired` → `RequireManualAction`, pausa conector, notifica;
   - `PostingClosed` → `MarkFailed` + `JobPosting.Close()`;
   - demais → `MarkFailed(error)`.
5. Entre envios do mesmo conector, delay de cortesia maior (`Connectors:<Key>:SubmissionDelayMin/Max`).

## 6. `ConnectorInvoker`

Ponto único de chamada do núcleo para conectores. É *scoped* (lê `ConnectorState`) e não grava estado: quem chama decide o que registrar.

Ordem: pausa em `ConnectorState` (`RateLimited` com código `Connector.Paused`, sem chamar o conector) → lease do rate limit → espaçamento de cortesia → pipeline Polly → métricas. A pipeline opera sobre um envelope `ConnectorCallOutcome(Error, Result)`, uma por conector e tipo de operação, independente do `T` do resultado. Exceções viram `Transient`: `Connector.CircuitOpen`, `Connector.Timeout` ou `Connector.Unhandled`.

O trecho abaixo é a versão simplificada da ideia:

```csharp
internal sealed class ConnectorInvoker(
    ConnectorRateLimiter rateLimiter,
    ResiliencePipelineProvider<string> pipelines,
    JobHunterMetrics metrics,
    ILogger<ConnectorInvoker> logger)
{
    public async Task<ConnectorResult<T>> InvokeAsync<T>(
        string connectorKey,
        Func<CancellationToken, Task<ConnectorResult<T>>> operation,
        CancellationToken ct,
        ConnectorOperationKind kind = ConnectorOperationKind.Read)
    {
        using var lease = await rateLimiter.AcquireAsync(connectorKey, ct);
        if (!lease.IsAcquired)
            return ConnectorResult<T>.Fail(new(ConnectorErrorKind.RateLimited, "Connector.RateLimited", "Local rate limit reached"));

        await rateLimiter.PolitenessDelayAsync(connectorKey, kind, ct);

        var pipeline = pipelines.GetPipeline<ConnectorResult<T>>($"connector:{connectorKey}:{kind}");
        var started = Stopwatch.GetTimestamp();

        try
        {
            var result = await pipeline.ExecuteAsync(async token => await operation(token), ct);
            metrics.RecordConnectorCall(connectorKey, kind, result.Error?.Kind, Stopwatch.GetElapsedTime(started));
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unhandled exception from connector {Connector}", connectorKey);
            metrics.RecordConnectorCall(connectorKey, kind, ConnectorErrorKind.Transient, Stopwatch.GetElapsedTime(started));
            return ConnectorResult<T>.Fail(new(ConnectorErrorKind.Transient, "Connector.Unhandled", ex.Message));
        }
    }
}
```

Pipelines registrados por conector habilitado na inicialização:

| Pipeline | Estratégias |
|---|---|
| `connector:{key}:Read` | Timeout total → Retry (2×, exponencial com jitter, só `Transient`/`RateLimited` respeitando `RetryAfter`) → Circuit breaker (falha ≥ 50% em 10 chamadas, quebra 15 min) → Timeout por tentativa |
| `connector:{key}:Submit` | Somente timeout. Sem retry, sem hedging |

O circuit breaker trata como falha `Transient` e `LayoutChanged`; `PostingClosed`, `NotFound`, `InvalidInput` são resultados normais.

## 7. `ConnectorRateLimiter`

- `PartitionedRateLimiter<string>` com partição por chave de conector: `ConcurrencyLimiter` (`MaxConcurrency`) + `TokenBucketRateLimiter` (`RequestsPerMinute`).
- `PolitenessDelayAsync`: espaçamento com jitter **entre chamadas consecutivas** do mesmo conector e tipo (leitura ou envio). A primeira chamada não espera; as seguintes aguardam só o que falta do intervalo sorteado entre `min` e `max` (`Task.Delay(…, clock, ct)`). Assim um `submit-now` isolado não fica minutos parado.
- O `BrowserProvider` aplica ainda o limite global de páginas (`Browser:MaxConcurrentPages`).

## 8. Instância única

O projeto assume **um processo**. Proteções contra trabalho duplicado (por exemplo, `dotnet run` aberto duas vezes):
- índice único em `applications.job_posting_id`;
- `xmin` em `Application` e `SourceTarget`;
- transição `Approved → Submitting` com `SaveChanges` antes de chamar o conector.

Se um dia houver mais de uma instância, migrar a seleção de trabalho para `SELECT ... FOR UPDATE SKIP LOCKED` (ver `docs/02-arquitetura.md` §9).

## 9. Desligamento

- Todos os `CancellationToken` propagam `stoppingToken`.
- `BrowserProvider` implementa `IAsyncDisposable` e fecha o Chromium.
- `HostOptions.ShutdownTimeout` = 30 s para concluir um envio em andamento; o estado `Submitting` órfão é detectado no próximo início e movido para `Failed` com `last_error_kind = Interrupted` (você decide se reenvia após checar a evidência e o e-mail).
