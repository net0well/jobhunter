# 07 — Conectores

Este documento define os **contratos** que toda plataforma implementa, a **matriz de conectores** e o **checklist** para adicionar uma plataforma nova. Detalhes de cada conector ficam em `07.N-conector-<plataforma>.md` (o primeiro é `07.1-conector-inhire.md`).

## 1. Princípios

1. **Capacidades pequenas.** Um conector implementa só o que a plataforma permite e suporta. Nada de `NotSupportedException`.
2. **Conector descreve, núcleo decide.** O conector traduz a plataforma para DTOs neutros (vagas, formulários). Pontuação, deduplicação, preenchimento, aprovação e limites ficam no núcleo.
3. **Sem vazamento de plataforma.** Nenhum tipo, URL, seletor ou nome de campo de plataforma sai do projeto do conector. Dados extras vão em `Extra` (dicionário), que o núcleo armazena e não interpreta.
4. **Falhas esperadas são valores.** Conectores retornam `ConnectorResult` com `ConnectorError` tipado.
5. **Stateless entre chamadas.** Estado (sessões, checkpoints, cursores) é devolvido ao núcleo ou guardado em arquivo de sessão gerenciado pela infraestrutura, nunca em campos de instância.
6. **Chave estável.** `Descriptor.Key` (`"inhire"`) é gravada no banco. Nunca muda depois de publicada.

## 2. Contratos (`JobHunter.Connectors.Abstractions`)

### 2.1 Descritor

```csharp
public interface IJobConnector
{
    ConnectorDescriptor Descriptor { get; }
}

public sealed record ConnectorDescriptor(
    string Key,
    string DisplayName,
    ConnectorCapabilities Capabilities,
    ConnectorTraits Traits);

[Flags]
public enum ConnectorCapabilities
{
    None = 0,
    TenantListing = 1 << 0,
    Search = 1 << 1,
    Feed = 1 << 2,
    PostingDetails = 1 << 3,
    ApplicationInspect = 1 << 4,
    ApplicationSubmit = 1 << 5,
    AssistedApply = 1 << 6,
    TenantDiscovery = 1 << 7,
    HealthProbe = 1 << 8,
}

public sealed record ConnectorTraits(
    bool RequiresAuthentication,
    bool UsesBrowser,
    TimeSpan DefaultScanInterval,
    int DefaultMaxConcurrency,
    TimeSpan DefaultPolitenessDelayMin,
    TimeSpan DefaultPolitenessDelayMax);
```

Os `Default*` são sugestões do conector; a configuração `Connectors:<Key>:*` sempre prevalece.

### 2.2 Resultado e erros

`JobHunter.Connectors.Abstractions` tem seu próprio tipo de resultado porque conectores não podem depender do `Result` do núcleo. O núcleo traduz `ConnectorError` para `Error` em `Common/Connectors/ConnectorErrorMapper.cs`.

```csharp
public enum ConnectorErrorKind
{
    Transient,
    RateLimited,
    LayoutChanged,
    FormChanged,
    AuthenticationRequired,
    CaptchaDetected,
    PostingClosed,
    NotFound,
    InvalidInput,
    Unsupported,
}

public sealed record ConnectorError(
    ConnectorErrorKind Kind,
    string Code,
    string Message,
    string? EvidencePath = null,
    TimeSpan? RetryAfter = null);

public readonly record struct ConnectorResult<T>
{
    public T? Value { get; }
    public ConnectorError? Error { get; }
    public bool IsSuccess => Error is null;

    public static ConnectorResult<T> Ok(T value) => new(value, null);
    public static ConnectorResult<T> Fail(ConnectorError error) => new(default, error);

    private ConnectorResult(T? value, ConnectorError? error) => (Value, Error) = (value, error);
}
```

| Kind | Significado | Reação do núcleo |
|---|---|---|
| `Transient` | Timeout, rede, 5xx | Retry pelo pipeline Polly |
| `RateLimited` | Plataforma pediu para desacelerar | Respeita `RetryAfter`, pausa o conector |
| `LayoutChanged` | Parser não encontrou estrutura esperada | Abre circuito do conector, notifica, guarda evidência |
| `FormChanged` | Formulário mudou entre inspeção e envio | Candidatura volta para `PendingReview` com novo formulário |
| `AuthenticationRequired` | Sessão ausente/expirada | `RequiresManualAction`, notifica |
| `CaptchaDetected` | Proteção anti-bot | `RequiresManualAction`, pausa o conector no ciclo |
| `PostingClosed` | Vaga encerrada | `JobPosting.Close()` |
| `NotFound` | Tenant ou vaga inexistente | Desativa a fonte após N ocorrências |
| `InvalidInput` | Entrada inválida (ex.: tenant mal formatado) | 400 na API |
| `Unsupported` | Operação não suportada naquele caso específico | Marca `ManualApplyOnly` |

### 2.3 Referências e DTOs de vaga

```csharp
public sealed record TenantRef(string Value);

public sealed record SourcedPosting(
    string ExternalId,
    Uri SourceUrl,
    string Title,
    string? CompanyName,
    string? Location,
    WorkModel? WorkModel,
    string? Seniority,
    string? EmploymentType,
    DateTimeOffset? PublishedAt,
    string? DescriptionText,
    Uri? ApplyUrl,
    IReadOnlyDictionary<string, string> Extra);

public sealed record PostingDetails(
    string ExternalId,
    string Title,
    string? DescriptionHtml,
    string DescriptionText,
    IReadOnlyList<string> Requirements,
    Uri? ApplyUrl,
    bool IsOpen,
    IReadOnlyDictionary<string, string> Extra);

public enum WorkModel { OnSite, Hybrid, Remote }
```

Regras:
- `ExternalId` é **estável** para a mesma vaga entre varreduras. Se a plataforma não expõe id, o conector deriva de algo estável (slug da URL); nunca de posição na lista.
- `DescriptionText` é texto puro (sem HTML), usado pela pontuação.
- `Extra` aceita apenas strings curtas; nada de PII.

### 2.4 Capacidades de captura

```csharp
public interface ITenantJobSource
{
    ConnectorResult<TenantRef> ParseTenant(string input);
    Uri GetTenantUrl(TenantRef tenant);
    Task<ConnectorResult<IReadOnlyList<SourcedPosting>>> ListAsync(TenantRef tenant, CancellationToken ct);
}

public interface ISearchJobSource
{
    Task<ConnectorResult<SearchPage>> SearchAsync(SearchCriteria criteria, string? cursor, CancellationToken ct);
}

public sealed record SearchCriteria(
    IReadOnlyList<string> Keywords,
    string? Location,
    IReadOnlyList<WorkModel> WorkModels,
    int MaxResults);

public sealed record SearchPage(IReadOnlyList<SourcedPosting> Items, string? NextCursor);

public interface IFeedJobSource
{
    Task<ConnectorResult<FeedBatch>> PullAsync(string? checkpoint, CancellationToken ct);
}

public sealed record FeedBatch(IReadOnlyList<SourcedPosting> Items, string? NextCheckpoint);

public interface IJobDetailsProvider
{
    bool CanHandle(Uri postingUrl);
    Task<ConnectorResult<PostingDetails>> GetDetailsAsync(Uri postingUrl, CancellationToken ct);
}

public interface ITenantDiscovery
{
    Task<ConnectorResult<IReadOnlyList<DiscoveredTenant>>> DiscoverAsync(string query, CancellationToken ct);
}

public sealed record DiscoveredTenant(TenantRef Tenant, string? CompanyName, Uri Url);
```

`ParseTenant` aceita o que o usuário cola: a URL completa (`https://eyxo.inhire.app/vagas`) ou só o identificador (`eyxo`). O núcleo nunca faz parsing de URL de plataforma.

O cursor de busca e o checkpoint de feed são **opacos** para o núcleo, que apenas os persiste em `SourceTarget.Checkpoint`.

### 2.5 Capacidades de candidatura

```csharp
public interface IApplicationChannel
{
    bool CanHandle(Uri applyUrl);
    Task<ConnectorResult<ApplicationForm>> InspectAsync(Uri applyUrl, CancellationToken ct);
    Task<ConnectorResult<SubmissionReceipt>> SubmitAsync(ApplicationSubmission submission, CancellationToken ct);
}

public interface IAssistedApplicationChannel
{
    Task<ConnectorResult<bool>> OpenAssistedAsync(ApplicationSubmission submission, CancellationToken ct);
}

public sealed record ApplicationForm(string FormHash, IReadOnlyList<FormField> Fields);

public sealed record FormField(
    string Key,
    string Label,
    FieldKind Kind,
    FieldSemantic Semantic,
    bool Required,
    IReadOnlyList<FieldOption> Options,
    int? MaxLength,
    string? HelpText);

public sealed record FieldOption(string Value, string Label);

public enum FieldKind
{
    Text, TextArea, Email, Phone, Url, Number, Date,
    SingleChoice, MultipleChoice, Boolean, File,
}

public enum FieldSemantic
{
    Custom,
    FullName, FirstName, LastName, Email, Phone,
    City, State, Country,
    LinkedInUrl, GitHubUrl, PortfolioUrl,
    Resume, CoverLetter,
    SalaryExpectation, AvailabilityDate,
    ConsentTerms, ConsentTalentPool,
}

public sealed record ApplicationSubmission(
    Uri ApplyUrl,
    string FormHash,
    IReadOnlyList<FieldValue> Values,
    string EvidenceDirectory);

public sealed record FieldValue(
    string Key,
    string? Text,
    IReadOnlyList<string>? SelectedOptions,
    string? FilePath);

public sealed record SubmissionReceipt(
    DateTimeOffset SubmittedAt,
    string? ConfirmationText,
    string? ExternalApplicationId,
    string? EvidencePath);
```

Regras:
- `Key` do campo é estável enquanto o formulário não muda (ex.: atributo `name` do input). `FormHash` é o hash da lista ordenada de `(Key, Kind, Required, Options)`.
- O conector mapeia `Semantic` com base em sinais da própria plataforma (tipo do input, `name`, label). Na dúvida, `Custom`: o núcleo trata como pergunta e você revisa.
- `ConsentTerms` **nunca** é marcado automaticamente pelo núcleo sem estar na revisão: aparece destacado e exige aprovação.
- `SubmitAsync` **recalcula** o `FormHash`; se diferente, retorna `FormChanged` sem enviar nada.
- `SubmitAsync` só retorna sucesso se detectar confirmação (mensagem, redirecionamento ou resposta da API). Caso contrário retorna erro com evidência.

### 2.6 Saúde (opcional)

```csharp
public interface IConnectorHealthProbe
{
    Task<ConnectorResult<bool>> ProbeAsync(CancellationToken ct);
}
```

Verificação leve (ex.: abrir uma página pública conhecida e checar um seletor). Usada pelo health check `connectors` e antes de reabrir um circuito.

## 3. Registro (composition root)

Cada conector expõe um único método de extensão. Implementações por capacidade podem ser classes separadas; o conector registra cada uma como **keyed service** com sua chave.

```csharp
public static class InHireConnectorRegistration
{
    public const string Key = "inhire";

    public static IServiceCollection AddInHireConnector(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection($"Connectors:{Key}");
        if (!section.GetValue<bool>("Enabled"))
            return services;

        services.AddOptions<InHireOptions>().Bind(section).ValidateDataAnnotations().ValidateOnStart();
        services.AddHttpClient(HttpClientName, (provider, http) => { /* base, timeout, User-Agent */ });

        services.AddSingleton<InHireApiClient>();
        services.AddSingleton<InHireConnector>();
        services.AddSingleton<IJobConnector>(provider => provider.GetRequiredService<InHireConnector>());
        services.AddKeyedSingleton<ITenantJobSource, InHireTenantSource>(Key);
        services.AddKeyedSingleton<IJobDetailsProvider, InHireDetailsProvider>(Key);
        services.AddKeyedSingleton<IApplicationChannel, InHireApplicationChannel>(Key);

        return services;
    }
}
```

No núcleo, `Common/Connectors/ConnectorRegistration.cs` é o **único** arquivo que conhece conectores concretos:

```csharp
internal static class ConnectorRegistration
{
    public static IServiceCollection AddConnectors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddInHireConnector(configuration);
        // services.AddGreenhouseConnector(configuration);
        // services.AddLinkedInAlertsConnector(configuration);

        services.AddSingleton<IConnectorRegistry, ConnectorRegistry>();
        services.AddSingleton<ConnectorInvoker>();
        services.AddSingleton<ConnectorRateLimiter>();
        services.AddSingleton<ApplyChannelResolver>();
        services.AddHostedService<ConnectorRegistryValidator>();
        return services;
    }
}
```

`ConnectorRegistryValidator` roda na inicialização e **falha o startup** se:
- duas implementações de `IJobConnector` usam a mesma `Key`;
- uma capacidade declarada no descritor não tem keyed service correspondente (ou o contrário);
- a `Key` não é minúscula `[a-z0-9-]+`.

### Uso pelo núcleo

```csharp
internal sealed class ScanSourceHandler(
    AppDbContext db,
    IConnectorRegistry connectors,
    ConnectorInvoker invoker,
    ISender sender) : IRequestHandler<ScanSource.Command, Result<ScanSource.Response>>
{
    public async ValueTask<Result<ScanSource.Response>> Handle(ScanSource.Command request, CancellationToken ct)
    {
        var source = await db.SourceTargets.FindAsync([request.SourceTargetId], ct);
        if (source is null)
            return Result.Failure<ScanSource.Response>(Error.NotFound("Source.NotFound", "Source not found"));

        if (!connectors.TryGet<ITenantJobSource>(source.ConnectorKey, out var tenantSource))
            return Result.Failure<ScanSource.Response>(Error.Conflict("Connector.CapabilityMissing", "Connector cannot list tenants"));

        var listing = await invoker.InvokeAsync(
            source.ConnectorKey,
            token => tenantSource.ListAsync(new TenantRef(source.TenantRef!), token),
            ct);

        // ...upsert via sender.Send(new UpsertPostings.Command(...)), registrar ScanRun
    }
}
```

## 4. Infraestrutura compartilhada para conectores de navegador (`JobHunter.Connectors.Browser`)

| Tipo | Responsabilidade |
|---|---|
| `BrowserProvider` | Singleton; lança o Chromium sob demanda; `SemaphoreSlim` global de páginas; fecha no shutdown |
| `BrowserSession` | `await using` — cria `IBrowserContext` (com `storage-state` opcional por conector), `IPage`, timeouts das options |
| `ArtifactWriter` | Screenshot + HTML em `Artifacts/<connector>/<yyyy-MM-dd>/<operação>-<id>.{png,html}` |
| `HtmlFormReader` | Lê `<form>` (via AngleSharp ou `page.EvaluateAsync`) e produz `FormField` "crus" que o conector refina com semântica |
| `FormFiller` | Preenche `FieldValue` em uma página por `Key` usando os locators fornecidos pelo conector |
| `FixtureRouter` | Para testes: intercepta requisições com `page.RouteAsync` e responde com arquivos de fixture (conectores HTTP usam `FixtureHttpHandler` do kit) |
| `BrowserOptions` | `Headless`, `MaxConcurrentPages`, `DefaultTimeout`, `SlowMo` |

Conectores HTTP **não** referenciam este projeto.

## 5. Estrutura interna de um conector

```
src/JobHunter.Connectors.<Plataforma>/
  <Plataforma>Connector.cs              # IJobConnector + ConnectorDescriptor
  <Plataforma>ConnectorRegistration.cs  # Add<Plataforma>Connector
  <Plataforma>Options.cs                # options com DataAnnotations
  <Plataforma>Urls.cs                   # parsing de tenant, montagem de URL, CanHandle
  <Plataforma>Selectors.cs              # (se navegador) todos os seletores
  Sources/<Plataforma>TenantSource.cs   # ITenantJobSource (ou SearchSource / FeedSource)
  Details/<Plataforma>DetailsProvider.cs
  Applications/<Plataforma>ApplicationChannel.cs
  Applications/<Plataforma>FieldSemantics.cs   # mapeia campos da plataforma -> FieldSemantic
  Parsing/<Plataforma>ListingParser.cs  # puro, testável
  Parsing/<Plataforma>DetailsParser.cs
  Parsing/<Plataforma>Models.cs         # DTOs internos de JSON/HTML (internal)
  GlobalUsings.cs

tests/JobHunter.Connectors.<Plataforma>.Tests/
  Fixtures/                             # HTML/JSON reais salvos (sem PII)
  Parsing/<Plataforma>ListingParserTests.cs
  Parsing/<Plataforma>DetailsParserTests.cs
  Applications/<Plataforma>FieldSemanticsTests.cs
  Contract/<Plataforma>ContractTests.cs # herda do kit
  Live/<Plataforma>LiveTests.cs         # [Trait("Category","Live")], desligado por padrão
```

Tudo `internal`, exceto a classe de registro e as options. Adicione `InternalsVisibleTo` para o projeto de testes.

## 6. Kit de testes de contrato (`tests/JobHunter.Connectors.Testing`)

Classes base abstratas que cada conector herda, fornecendo uma fábrica do serviço e fixtures. Todo conector novo precisa passar nelas.

| Classe base | O que garante |
|---|---|
| `ConnectorDescriptorContract` | `Key` válida e estável; capacidades declaradas == serviços registrados; traits coerentes (`UsesBrowser` ⇒ depende de `BrowserProvider`) |
| `TenantJobSourceContract` | `ParseTenant` aceita URL e id, rejeita lixo com `InvalidInput`; `ListAsync` sobre fixtures retorna itens com `ExternalId`, `Title`, `SourceUrl` preenchidos; `ExternalId` estável entre duas execuções; lista vazia é sucesso, não erro |
| `JobDetailsProviderContract` | `CanHandle` aceita URLs próprias e rejeita amostras de outras plataformas; `DescriptionText` sem tags HTML; vaga encerrada → `PostingClosed` |
| `ApplicationChannelContract` | `CanHandle` correto; `InspectAsync` retorna `FormHash` determinístico; campos obrigatórios marcados; pelo menos `Email` ou `Resume` com semântica reconhecida; `SubmitAsync` com `FormHash` diferente → `FormChanged` sem efeitos; nenhum método lança exceção para entrada inválida |
| `ErrorHandlingContract` | HTML inesperado → `LayoutChanged` com `EvidencePath`; página de captcha (fixture) → `CaptchaDetected` |

O kit também fornece:
- `FakeConnector` configurável (todas as capacidades), usado nos testes de integração do núcleo;
- `SampleUrls` com URLs de exemplo de várias plataformas para testar `CanHandle` cruzado;
- builders de `SourcedPosting` e `ApplicationForm`.

## 7. Matriz de conectores

Preencha antes de implementar qualquer conector. "Termos" registra o que você leu nos termos de uso da plataforma e a data da leitura.

| Key | Plataforma | Status | Captura | Detalhes | Candidatura | Assistido | Descoberta | Tecnologia | Auth | Termos de uso |
|---|---|---|---|---|---|---|---|---|---|---|
| `inhire` | InHire | **Captura** (envio desligado) | Tenant | Sim | A definir (spec 004/005) | A definir | Futuro | HTTP na API pública da página (ADR-009) | Não | 2026-10-30: termos não encontrados; robots.txt libera `*`; captura com cortesia, envio só após verificar os termos |
| `greenhouse` | Greenhouse | Futuro | Tenant (API pública de job board) | Sim (API) | A avaliar | Sim | Não | HTTP | Não | A verificar |
| `lever` | Lever | Futuro | Tenant (API pública de postings) | Sim (API) | A avaliar | Sim | Não | HTTP | Não | A verificar |
| `gupy` | Gupy | Futuro | Tenant | Sim | A avaliar | Sim | A avaliar | Playwright | Provável (conta de candidato) | A verificar |
| `linkedin-alerts` | LinkedIn (alertas por e-mail) | Futuro | Feed | Não (usa canal de destino) | **Não** | Não | Não | Leitura de e-mail | Caixa de e-mail | O Contrato do Usuário proíbe bots e scraping: somente captura via e-mails de alerta que você já recebe |
| `manual` | Importação por URL | Núcleo | Feed manual | Via conector que reconhecer a URL | Via canal resolvido | — | — | — | — | — |

## 8. Como adicionar um conector (checklist)

1. **Pesquisa e termos.** Leia os termos de uso. Preencha a linha da matriz (seção 7). Se a plataforma proíbe automação, limite a captura a meios permitidos (feed/e-mail/URL manual) e **não** implemente canal de envio.
2. **Reconhecimento.** Crie `docs/07.N-conector-<plataforma>.md` com o mesmo roteiro de `07.1-conector-inhire.md`. Prefira API pública a navegador.
3. **Especificação.** Descreva escopo, capacidades e critérios de aceite antes de implementar.
4. **Projetos.**
   ```bash
   dotnet new classlib -n JobHunter.Connectors.<Plataforma> -o src/JobHunter.Connectors.<Plataforma>
   dotnet new xunit3 -n JobHunter.Connectors.<Plataforma>.Tests -o tests/JobHunter.Connectors.<Plataforma>.Tests
   dotnet sln add src/JobHunter.Connectors.<Plataforma> tests/JobHunter.Connectors.<Plataforma>.Tests
   ```
   Referências: `Abstractions` (+ `Browser` se usar navegador). Testes referenciam o conector e `Connectors.Testing`.
5. **Descritor e registro.** `<Plataforma>Connector` + `Add<Plataforma>Connector`.
6. **Parsers primeiro, com fixtures** (TDD). Salve HTML/JSON reais **sem dados pessoais**.
7. **Capacidades.** Implemente só as declaradas.
8. **Contrato.** Herde as classes do kit; todos os testes verdes.
9. **Composição.** Uma linha em `Common/Connectors/ConnectorRegistration.cs` + seção `Connectors:<Plataforma>` no `appsettings.json` com `Enabled: false`.
10. **Habilitar gradualmente.** Primeiro só captura; depois inspeção de formulário; envio por último, com `DailySubmissionLimit` baixo.
11. **Verificar.** build, testes e testes de arquitetura (nenhuma referência ao conector fora do composition root).

## 9. Versionamento do contrato

- Adicionar uma **nova capacidade** (nova interface + flag) é não-quebrante.
- Adicionar **campo opcional** a um DTO (com default) é não-quebrante.
- Mudar assinatura de método existente é **quebrante**: atualize todos os conectores e o kit no mesmo PR.
- Novos valores em `FieldSemantic` são não-quebrantes; o núcleo trata desconhecidos como `Custom`.
