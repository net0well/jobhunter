# 04 — Estrutura da solução

Convenções: `.slnx`, `Directory.Build.props`, `Directory.Packages.props` (gestão central de pacotes), `global.json`, separação `src/` e `tests/`.

## 1. Árvore completa

```
JobHunter/
├── JobHunter.slnx
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── .editorconfig
├── .gitignore
├── .gitattributes
├── docker-compose.yml
├── README.md
├── .githooks/
│   └── commit-msg
├── .github/
│   └── workflows/
│       └── ci.yml
├── docs/                                       # este pacote
│
├── src/
│   ├── JobHunter.Api/
│   │   ├── JobHunter.Api.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Properties/launchSettings.json
│   │   │
│   │   ├── Features/
│   │   │   ├── Connectors/
│   │   │   │   ├── ConnectorEndpoints.cs
│   │   │   │   └── ListConnectors.cs
│   │   │   ├── Sources/
│   │   │   │   ├── SourceEndpoints.cs
│   │   │   │   ├── AddSource.cs
│   │   │   │   ├── ImportSources.cs
│   │   │   │   ├── UpdateSource.cs
│   │   │   │   ├── ListSources.cs
│   │   │   │   ├── GetSource.cs
│   │   │   │   ├── DeleteSource.cs
│   │   │   │   └── TriggerSourceScan.cs
│   │   │   ├── Scanning/
│   │   │   │   ├── ScanEndpoints.cs
│   │   │   │   ├── ScanDueSources.cs
│   │   │   │   ├── ScanSource.cs
│   │   │   │   ├── ListScanRuns.cs
│   │   │   │   └── Shared/ScanRequestQueue.cs      # fila das varreduras disparadas pela API
│   │   │   ├── Companies/
│   │   │   │   ├── CompanyEndpoints.cs
│   │   │   │   ├── ListCompanies.cs
│   │   │   │   └── SetCompanyBlocked.cs
│   │   │   ├── Postings/
│   │   │   │   ├── PostingEndpoints.cs
│   │   │   │   ├── ImportPostingFromUrl.cs
│   │   │   │   ├── ListPostings.cs
│   │   │   │   ├── GetPosting.cs
│   │   │   │   ├── DiscardPosting.cs
│   │   │   │   ├── RestorePosting.cs
│   │   │   │   ├── RefreshPostingDetails.cs
│   │   │   │   └── Shared/
│   │   │   │       ├── PostingUpserter.cs              # upsert + deduplicação + canal
│   │   │   │       └── PostingDetail.cs / PostingSummary.cs
│   │   │   ├── Matching/
│   │   │   │   ├── SearchProfileEndpoints.cs
│   │   │   │   ├── ScorePendingPostings.cs
│   │   │   │   ├── RescoreAll.cs
│   │   │   │   ├── CreateSearchProfile.cs
│   │   │   │   ├── UpdateSearchProfile.cs
│   │   │   │   ├── ListSearchProfiles.cs
│   │   │   │   ├── DeleteSearchProfile.cs
│   │   │   │   └── Shared/
│   │   │   │       └── MatchScorer.cs
│   │   │   ├── Applications/
│   │   │   │   ├── ApplicationEndpoints.cs
│   │   │   │   ├── PrepareApplication.cs
│   │   │   │   ├── PreparePendingApplications.cs
│   │   │   │   ├── ListApplications.cs
│   │   │   │   ├── GetApplication.cs
│   │   │   │   ├── UpdateAnswers.cs
│   │   │   │   ├── ApproveApplication.cs
│   │   │   │   ├── SkipApplication.cs
│   │   │   │   ├── RetryApplication.cs
│   │   │   │   ├── SubmitApplication.cs
│   │   │   │   ├── SubmitApprovedApplications.cs
│   │   │   │   ├── OpenAssisted.cs
│   │   │   │   ├── MarkSubmittedManually.cs
│   │   │   │   └── Shared/
│   │   │   │       ├── FormAnswerer.cs
│   │   │   │       └── SubmissionBuilder.cs
│   │   │   ├── Answers/
│   │   │   │   ├── AnswerTemplateEndpoints.cs
│   │   │   │   ├── CreateAnswerTemplate.cs
│   │   │   │   ├── UpdateAnswerTemplate.cs
│   │   │   │   ├── ListAnswerTemplates.cs
│   │   │   │   ├── DeleteAnswerTemplate.cs
│   │   │   │   ├── SaveAnswerAsTemplate.cs
│   │   │   │   └── Suggestions/
│   │   │   │       ├── IAnswerSuggester.cs
│   │   │   │       ├── TemplateAnswerSuggester.cs
│   │   │   │       └── AnthropicAnswerSuggester.cs      # opcional
│   │   │   ├── Profile/
│   │   │   │   ├── ProfileEndpoints.cs
│   │   │   │   ├── GetProfile.cs
│   │   │   │   ├── UpdateProfile.cs
│   │   │   │   └── UploadResume.cs
│   │   │   └── Discovery/
│   │   │       ├── DiscoveryEndpoints.cs
│   │   │       └── DiscoverTenants.cs
│   │   │
│   │   ├── Domain/
│   │   │   ├── Company.cs
│   │   │   ├── SourceTarget.cs
│   │   │   ├── JobPosting.cs
│   │   │   ├── PostingSource.cs
│   │   │   ├── SearchProfile.cs
│   │   │   ├── CandidateProfile.cs
│   │   │   ├── AnswerTemplate.cs
│   │   │   ├── Application.cs
│   │   │   ├── ApplicationField.cs
│   │   │   ├── ScanRun.cs
│   │   │   ├── ConnectorState.cs
│   │   │   └── Enums/
│   │   │       ├── JobPostingStatus.cs
│   │   │       ├── ApplicationStatus.cs
│   │   │       ├── SourceKind.cs
│   │   │       └── ProposalSource.cs
│   │   │
│   │   ├── Workers/
│   │   │   ├── PeriodicCommandWorker.cs            # base: um command por tick, um scope por ciclo
│   │   │   ├── ManualScanWorker.cs
│   │   │   ├── SourceScanWorker.cs
│   │   │   ├── MatchingWorker.cs
│   │   │   ├── PreparationWorker.cs
│   │   │   ├── SubmissionWorker.cs
│   │   │   └── MaintenanceWorker.cs
│   │   │
│   │   └── Common/
│   │       ├── Results/
│   │       │   ├── Result.cs
│   │       │   ├── Error.cs
│   │       │   ├── ErrorType.cs
│   │       │   └── ResultExtensions.cs                 # Result -> ProblemDetails
│   │       ├── Endpoints/
│   │       │   ├── IEndpointGroup.cs
│   │       │   ├── EndpointExtensions.cs               # MapEndpoints() auto-discovery
│   │       │   ├── ValidationFilter.cs
│   │       │   └── ApiKeyEndpointFilter.cs
│   │       ├── Errors/
│   │       │   └── GlobalExceptionHandler.cs
│   │       ├── Connectors/
│   │       │   ├── ConnectorRegistration.cs            # único lugar que conhece conectores concretos
│   │       │   ├── IConnectorRegistry.cs
│   │       │   ├── ConnectorRegistry.cs
│   │       │   ├── ConnectorRegistryValidator.cs
│   │       │   ├── ConnectorInvoker.cs                 # rate limit + Polly + métricas
│   │       │   ├── ConnectorPipelines.cs               # pipelines Polly por conector (read/submit)
│   │       │   ├── ConnectorCallOutcome.cs             # envelope único para as pipelines
│   │       │   ├── ConnectorCircuitStates.cs           # estado do circuito exposto em /api/connectors
│   │       │   ├── ConnectorRateLimiter.cs
│   │       │   ├── ConnectorErrorMapper.cs             # ConnectorError -> Error
│   │       │   ├── ApplyChannelResolver.cs
│   │       │   ├── ConfigureConnectorOptions.cs        # traits como default + Connectors:<Key>
│   │       │   ├── ValidateConnectorOptions.cs
│   │       │   └── ConnectorOptions.cs                 # Connectors:<Key> comuns
│   │       ├── Persistence/
│   │       │   ├── AppDbContext.cs
│   │       │   ├── Configurations/                     # um IEntityTypeConfiguration por entidade
│   │       │   ├── Interceptors/TimestampsInterceptor.cs
│   │       │   └── Migrations/
│   │       ├── Text/
│   │       │   ├── Normalizer.cs
│   │       │   └── UrlNormalizer.cs
│   │       ├── Artifacts/
│   │       │   └── ArtifactRetention.cs
│   │       ├── Notifications/
│   │       │   ├── INotifier.cs
│   │       │   └── LogNotifier.cs                      # Telegram/e-mail no futuro
│   │       ├── Options/
│   │       │   ├── AppOptions.cs
│   │       │   ├── SchedulingOptions.cs
│   │       │   ├── ApplicationsOptions.cs
│   │       │   ├── ArtifactsOptions.cs
│   │       │   └── LlmOptions.cs
│   │       ├── Observability/
│   │       │   ├── JobHunterMetrics.cs
│   │       │   └── ActivitySources.cs
│   │       ├── Health/
│   │       │   ├── ConnectorsHealthCheck.cs
│   │       │   └── BrowserHealthCheck.cs
│   │       └── Extensions/
│   │           ├── ServiceCollectionExtensions.cs
│   │           └── WebApplicationExtensions.cs
│   │
│   ├── JobHunter.Connectors.Abstractions/
│   │   ├── JobHunter.Connectors.Abstractions.csproj
│   │   ├── IJobConnector.cs
│   │   ├── ConnectorDescriptor.cs
│   │   ├── ConnectorCapabilities.cs
│   │   ├── ConnectorTraits.cs
│   │   ├── ConnectorCapabilityServices.cs          # capacidade -> interface de serviço
│   │   ├── Results/
│   │   │   ├── ConnectorResult.cs
│   │   │   ├── ConnectorError.cs
│   │   │   └── ConnectorErrorKind.cs
│   │   ├── Sources/
│   │   │   ├── ITenantJobSource.cs
│   │   │   ├── ISearchJobSource.cs
│   │   │   ├── IFeedJobSource.cs
│   │   │   ├── IJobDetailsProvider.cs
│   │   │   ├── ITenantDiscovery.cs
│   │   │   ├── TenantRef.cs
│   │   │   ├── SourcedPosting.cs
│   │   │   ├── PostingDetails.cs
│   │   │   ├── SearchCriteria.cs
│   │   │   ├── SearchPage.cs
│   │   │   ├── FeedBatch.cs
│   │   │   ├── DiscoveredTenant.cs
│   │   │   └── WorkModel.cs
│   │   ├── Applications/
│   │   │   ├── IApplicationChannel.cs
│   │   │   ├── IAssistedApplicationChannel.cs
│   │   │   ├── ApplicationForm.cs
│   │   │   ├── FormField.cs
│   │   │   ├── FieldOption.cs
│   │   │   ├── FieldKind.cs
│   │   │   ├── FieldSemantic.cs
│   │   │   ├── FieldValue.cs
│   │   │   ├── ApplicationSubmission.cs
│   │   │   ├── SubmissionReceipt.cs
│   │   │   └── FormHasher.cs
│   │   └── Health/
│   │       └── IConnectorHealthProbe.cs
│   │
│   ├── JobHunter.Connectors.Browser/
│   │   ├── JobHunter.Connectors.Browser.csproj
│   │   ├── BrowserRegistration.cs                      # AddBrowserInfrastructure (idempotente, TryAdd)
│   │   ├── BrowserOptions.cs
│   │   ├── BrowserProvider.cs
│   │   ├── BrowserSession.cs
│   │   ├── SessionStateStore.cs                        # storage-state por conector
│   │   ├── ArtifactWriter.cs
│   │   ├── Forms/
│   │   │   ├── HtmlFormReader.cs
│   │   │   └── FormFiller.cs
│   │   ├── Detection/
│   │   │   └── CaptchaDetector.cs
│   │   └── Testing/
│   │       └── FixtureRouter.cs                        # usado só pelos testes (internal + InternalsVisibleTo)
│   │
│   └── JobHunter.Connectors.InHire/
│       ├── JobHunter.Connectors.InHire.csproj
│       ├── InHireConnector.cs
│       ├── InHireConnectorRegistration.cs
│       ├── InHireOptions.cs
│       ├── InHireUrls.cs
│       ├── InHireSelectors.cs
│       ├── Sources/InHireTenantSource.cs
│       ├── Details/InHireDetailsProvider.cs
│       ├── Applications/
│       │   ├── InHireApplicationChannel.cs
│       │   └── InHireFieldSemantics.cs
│       ├── Health/InHireHealthProbe.cs
│       └── Parsing/
│           ├── InHireListingParser.cs
│           ├── InHireDetailsParser.cs
│           └── InHireModels.cs
│
└── tests/
    ├── JobHunter.Api.Tests/
    │   ├── JobHunter.Api.Tests.csproj
    │   ├── Infrastructure/
    │   │   ├── PostgresFixture.cs                      # Testcontainers
    │   │   ├── JobHunterApiFactory.cs                  # WebApplicationFactory, workers desligados, FakeConnector
    │   │   └── TestTimeProvider.cs                     # ou Microsoft.Extensions.TimeProvider.Testing
    │   ├── Architecture/
    │   │   └── DependencyRulesTests.cs
    │   ├── Features/
    │   │   ├── Sources/...
    │   │   ├── Scanning/...
    │   │   ├── Postings/...
    │   │   ├── Matching/...
    │   │   ├── Applications/...
    │   │   └── Answers/...
    │   └── Domain/
    │       ├── ApplicationTests.cs
    │       └── JobPostingTests.cs
    ├── JobHunter.Connectors.Testing/
    │   ├── JobHunter.Connectors.Testing.csproj
    │   ├── Contracts/
    │   │   ├── ConnectorDescriptorContract.cs
    │   │   ├── TenantJobSourceContract.cs
    │   │   ├── JobDetailsProviderContract.cs
    │   │   ├── ApplicationChannelContract.cs
    │   │   └── ErrorHandlingContract.cs
    │   ├── Fakes/
    │   │   └── FakeConnector.cs
    │   └── Builders/
    │       ├── SourcedPostingBuilder.cs
    │       ├── ApplicationFormBuilder.cs
    │       └── SampleUrls.cs
    └── JobHunter.Connectors.InHire.Tests/
        ├── JobHunter.Connectors.InHire.Tests.csproj
        ├── Fixtures/
        ├── Parsing/
        ├── Applications/
        ├── Contract/InHireContractTests.cs
        └── Live/InHireLiveTests.cs
```

`Domain/` fica no projeto do host: as entidades são compartilhadas entre várias features (não pertencem a um slice só) e têm comportamento (métodos que retornam `Result`). Isso é compatível com VSA: features são verticais, o modelo persistido é comum.

## 2. Arquivos de raiz

### `global.json`

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

Ajuste `version` para o SDK 10 instalado (`dotnet --list-sdks`).

### `Directory.Build.props`

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup Condition="'$(IsTestProject)' != 'true'">
    <InternalsVisibleTo Include="$(AssemblyName).Tests" />
  </ItemGroup>
</Project>
```

### `Directory.Packages.props`

Somente os **nomes** importam aqui: `X` é um marcador. Cada versão é resolvida com `dotnet add package <nome>` (sem `--version`) e copiada para cá, sem fixar versões de memória. Não deixe nenhum `X` no arquivo real.

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Host / API -->
    <PackageVersion Include="Mediator.Abstractions" Version="X" />
    <PackageVersion Include="Mediator.SourceGenerator" Version="X" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="X" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="X" />
    <PackageVersion Include="Scalar.AspNetCore" Version="X" />

    <!-- Dados -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="X" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="X" />
    <PackageVersion Include="EFCore.NamingConventions" Version="X" />
    <PackageVersion Include="AspNetCore.HealthChecks.NpgSql" Version="X" />

    <!-- Conectores -->
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="X" />
    <PackageVersion Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="X" />
    <PackageVersion Include="Microsoft.Extensions.Options.DataAnnotations" Version="X" />
    <PackageVersion Include="Microsoft.Playwright" Version="X" />
    <PackageVersion Include="AngleSharp" Version="X" />

    <!-- Resiliência e HTTP -->
    <PackageVersion Include="Microsoft.Extensions.Resilience" Version="X" />
    <PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="X" />

    <!-- Observabilidade -->
    <PackageVersion Include="Serilog.AspNetCore" Version="X" />
    <PackageVersion Include="Serilog.Sinks.File" Version="X" />
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="X" />
    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="X" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="X" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="X" />
    <PackageVersion Include="Npgsql.OpenTelemetry" Version="X" />

    <!-- Testes -->
    <PackageVersion Include="xunit.v3" Version="X" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="X" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="X" />
    <PackageVersion Include="Verify.XunitV3" Version="X" />
    <PackageVersion Include="Microsoft.Extensions.TimeProvider.Testing" Version="X" />
  </ItemGroup>
</Project>
```

Antes de adicionar qualquer pacote, confira a licença: MediatR 13+, MassTransit 9+, FluentAssertions 8+ e AutoMapper 15+ (comerciais). Este projeto não usa nenhum deles.

### `JobHunter.slnx`

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/JobHunter.Api/JobHunter.Api.csproj" />
    <Project Path="src/JobHunter.Connectors.Abstractions/JobHunter.Connectors.Abstractions.csproj" />
    <Project Path="src/JobHunter.Connectors.Browser/JobHunter.Connectors.Browser.csproj" />
    <Project Path="src/JobHunter.Connectors.InHire/JobHunter.Connectors.InHire.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/JobHunter.Api.Tests/JobHunter.Api.Tests.csproj" />
    <Project Path="tests/JobHunter.Connectors.Testing/JobHunter.Connectors.Testing.csproj" />
    <Project Path="tests/JobHunter.Connectors.InHire.Tests/JobHunter.Connectors.InHire.Tests.csproj" />
  </Folder>
</Solution>
```

### `.gitignore` (além do template `dotnet new gitignore`)

```
Artifacts/
sessions/
resume/
*.storage-state.json
appsettings.*.local.json
.env
```

### `.gitattributes`

```
* text=auto eol=lf
*.cs diff=csharp
*.sh text eol=lf
.githooks/* text eol=lf
```

### `.editorconfig` (pontos essenciais)

- `csharp_style_namespace_declarations = file_scoped:error`
- `csharp_style_prefer_primary_constructors = true:suggestion`
- `dotnet_style_prefer_collection_expression = true:suggestion`
- `csharp_style_var_when_type_is_apparent = true:suggestion`
- `dotnet_diagnostic.CA2007.severity = none` (sem `ConfigureAwait` em app ASP.NET Core)
- `dotnet_diagnostic.IDE0005.severity = warning` (usings não usados)

## 3. Referências entre projetos

| Projeto | Referencia |
|---|---|
| `JobHunter.Api` | `Connectors.Abstractions`, `Connectors.InHire` |
| `JobHunter.Connectors.Browser` | `Connectors.Abstractions` |
| `JobHunter.Connectors.InHire` | `Connectors.Abstractions`, `Connectors.Browser` |
| `JobHunter.Connectors.Testing` | `Connectors.Abstractions` |
| `JobHunter.Api.Tests` | `JobHunter.Api`, `Connectors.Testing` |
| `JobHunter.Connectors.InHire.Tests` | `Connectors.InHire`, `Connectors.Testing` |

O teste `DependencyRulesTests` garante (via `Assembly.GetReferencedAssemblies()` e reflexão sobre namespaces usados) que:
- `JobHunter.Connectors.Abstractions` não referencia Playwright, AngleSharp, EF Core nem ASP.NET Core;
- nenhum conector referencia `JobHunter.Api` nem outro conector;
- nenhum tipo em `JobHunter.Api.Features.*` ou `JobHunter.Api.Workers.*` usa tipos de `JobHunter.Connectors.InHire`.

## 4. Comandos de criação

```bash
dotnet new sln -n JobHunter --format slnx
dotnet new gitignore
dotnet new editorconfig

dotnet new web       -n JobHunter.Api                         -o src/JobHunter.Api
dotnet new classlib  -n JobHunter.Connectors.Abstractions     -o src/JobHunter.Connectors.Abstractions
dotnet new classlib  -n JobHunter.Connectors.Browser          -o src/JobHunter.Connectors.Browser
dotnet new classlib  -n JobHunter.Connectors.InHire           -o src/JobHunter.Connectors.InHire
dotnet new classlib  -n JobHunter.Connectors.Testing          -o tests/JobHunter.Connectors.Testing
dotnet new xunit3    -n JobHunter.Api.Tests                   -o tests/JobHunter.Api.Tests
dotnet new xunit3    -n JobHunter.Connectors.InHire.Tests     -o tests/JobHunter.Connectors.InHire.Tests

dotnet sln add src/*/*.csproj tests/*/*.csproj

dotnet add src/JobHunter.Api reference src/JobHunter.Connectors.Abstractions src/JobHunter.Connectors.InHire
dotnet add src/JobHunter.Connectors.Browser reference src/JobHunter.Connectors.Abstractions
dotnet add src/JobHunter.Connectors.InHire reference src/JobHunter.Connectors.Abstractions src/JobHunter.Connectors.Browser
dotnet add tests/JobHunter.Connectors.Testing reference src/JobHunter.Connectors.Abstractions
dotnet add tests/JobHunter.Api.Tests reference src/JobHunter.Api tests/JobHunter.Connectors.Testing
dotnet add tests/JobHunter.Connectors.InHire.Tests reference src/JobHunter.Connectors.InHire tests/JobHunter.Connectors.Testing
```

Remova `Class1.cs` gerado e os `TargetFramework` duplicados nos `.csproj` (herdados de `Directory.Build.props`).
