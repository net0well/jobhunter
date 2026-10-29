# 08 — Configuração

Options pattern com `ValidateDataAnnotations()` + `ValidateOnStart()`; `IOptionsMonitor<T>` nos workers (permite ajustar intervalos sem reiniciar); segredos via `dotnet user-secrets`.

## 1. `appsettings.json` (versionado, sem segredos nem dados pessoais)

```json
{
  "App": {
    "TimeZone": "America/Sao_Paulo",
    "Urls": "http://localhost:5080"
  },
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=jobhunter;Username=jobhunter"
  },
  "Api": {
    "RequireApiKey": true
  },
  "Workers": {
    "SourceScan":  { "Enabled": true, "TickInterval": "00:05:00", "BatchSize": 20 },
    "Matching":    { "Enabled": true, "TickInterval": "00:02:00", "BatchSize": 200 },
    "Preparation": { "Enabled": true, "TickInterval": "00:05:00", "BatchSize": 5 },
    "Submission":  { "Enabled": true, "TickInterval": "00:10:00", "BatchSize": 2 },
    "Maintenance": { "Enabled": true, "TickInterval": "01:00:00" }
  },
  "Scanning": {
    "MaxParallelConnectors": 2,
    "DeactivateAfterFailures": 5,
    "RunRetentionDays": 90
  },
  "Applications": {
    "AutoPrepare": true,
    "DailySubmissionLimit": 10,
    "SubmissionWindow": { "Start": "08:00", "End": "20:00" },
    "DefaultMode": "ReviewThenSubmit"
  },
  "Browser": {
    "Headless": true,
    "MaxConcurrentPages": 1,
    "DefaultTimeout": "00:00:30",
    "SlowMo": "00:00:00"
  },
  "Artifacts": {
    "RootPath": "../../Artifacts",
    "RetentionDays": 30,
    "SaveHtmlOnSuccess": false
  },
  "Profile": {
    "ResumeDirectory": "../../resume"
  },
  "Secrets": {
    "SessionsPath": "../../sessions"
  },
  "Llm": {
    "Enabled": false,
    "Provider": "Anthropic",
    "Model": "",
    "MaxTokens": 600,
    "Timeout": "00:00:30"
  },
  "Notifications": {
    "Provider": "Log"
  },
  "Connectors": {
    "InHire": {
      "Enabled": true,
      "ScanInterval": "06:00:00",
      "MaxConcurrency": 1,
      "RequestsPerMinute": 10,
      "PolitenessDelayMin": "00:00:04",
      "PolitenessDelayMax": "00:00:10",
      "SubmissionDelayMin": "00:02:00",
      "SubmissionDelayMax": "00:05:00",
      "DailySubmissionLimit": 5,
      "NavigationTimeout": "00:00:30",
      "ConfirmationTimeout": "00:00:20",
      "ClosedAfterMissedScans": 2
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
      }
    }
  },
  "OpenTelemetry": {
    "OtlpEndpoint": ""
  }
}
```

`Llm:Model` fica vazio de propósito: preencha com um modelo atual da Anthropic ao habilitar (consulte a documentação oficial; não fixe de memória).

## 2. Segredos (`dotnet user-secrets`)

```bash
cd src/JobHunter.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=jobhunter;Username=jobhunter;Password=<senha>"
dotnet user-secrets set "Api:ApiKey" "<chave-longa-aleatória>"
dotnet user-secrets set "Llm:ApiKey" "<chave-da-api>"                # só se Llm:Enabled
dotnet user-secrets set "Notifications:Telegram:BotToken" "<token>"   # futuro
```

Gerar a API key: `openssl rand -base64 32`.

Dados pessoais do candidato ficam no banco (`CandidateProfile`), preenchidos pela API `PUT /api/profile`, nunca em `appsettings`.

## 3. Classes de options

Uma por seção, em `Common/Options/` (núcleo) ou no projeto do conector (`InHireOptions`, `BrowserOptions`).

```csharp
public sealed class ApplicationsOptions
{
    public const string Section = "Applications";

    public bool AutoPrepare { get; init; } = true;

    [Range(0, 50)]
    public int DailySubmissionLimit { get; init; } = 10;

    [Required]
    public SubmissionWindowOptions SubmissionWindow { get; init; } = new();

    public ApplicationMode DefaultMode { get; init; } = ApplicationMode.ReviewThenSubmit;
}
```

Opções comuns a todos os conectores (`ConnectorOptions`) são lidas pelo núcleo em `Connectors:<Key>` e usadas pelo `ConnectorRateLimiter`/`ConnectorInvoker`; opções específicas (ex.: `ConfirmationTimeout`) são lidas pelo próprio conector na mesma seção.

```csharp
public sealed class ConnectorOptions
{
    public bool Enabled { get; init; }
    public TimeSpan? ScanInterval { get; init; }
    [Range(1, 10)] public int MaxConcurrency { get; init; } = 1;
    [Range(1, 600)] public int RequestsPerMinute { get; init; } = 10;
    public TimeSpan PolitenessDelayMin { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan PolitenessDelayMax { get; init; } = TimeSpan.FromSeconds(8);
    public TimeSpan SubmissionDelayMin { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan SubmissionDelayMax { get; init; } = TimeSpan.FromMinutes(5);
    [Range(0, 50)] public int DailySubmissionLimit { get; init; } = 5;
    [Range(1, 10)] public int ClosedAfterMissedScans { get; init; } = 2;
}
```

Registro com named options por conector: `services.AddOptions<ConnectorOptions>(key).Bind(configuration.GetSection($"Connectors:{sectionName}"))`. O nome da seção é declarado pelo conector (ex.: `InHire`) junto com a chave (`inhire`).

Validação cruzada (`IValidateOptions<T>`): `PolitenessDelayMin <= PolitenessDelayMax`; `SubmissionWindow.Start < End`; `Llm:ApiKey` obrigatório se `Llm:Enabled`.

## 4. Ambientes

| Ambiente | Diferenças |
|---|---|
| `Development` | Scalar habilitado; `Browser:Headless` pode ser `false`; logs `Debug` para `JobHunter.*` |
| `Testing` | Workers desligados; conector real desligado; `FakeConnector` registrado pela factory de testes |
| `Production` (uso diário) | Scalar desligado ou restrito a localhost; `Headless = true` |

Arquivos `appsettings.*.local.json` (ignorados pelo git) podem ser carregados opcionalmente para ajustes pessoais.
