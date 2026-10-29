# JobHunter

Aplicação pessoal em .NET 10 que captura vagas de várias plataformas, filtra as que combinam com o seu perfil, prepara a candidatura e envia **somente após a sua aprovação**.

A captura e o envio acontecem por **conectores plugáveis**. O primeiro conector é o InHire (`https://{empresa}.inhire.app/vagas`); outras plataformas entram como novos projetos de conector, sem alterar o núcleo.

```
Fontes monitoradas → Captura → Deduplicação → Pontuação → Preparação → Revisão humana → Envio → Registro
```

## Stack

ASP.NET Core (Minimal API + BackgroundServices) · EF Core + PostgreSQL · Playwright · AngleSharp · Polly · Serilog · OpenTelemetry · xUnit v3 + Testcontainers

## Pré-requisitos

- .NET 10 SDK
- Docker
- PowerShell 7 (`pwsh`), para instalar o navegador do Playwright

## Primeiro uso

```bash
cp .env.example .env                 # defina POSTGRES_PASSWORD (e POSTGRES_PORT, se a 5432 estiver ocupada)
docker compose up -d

cd src/JobHunter.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=<POSTGRES_PORT>;Database=jobhunter;Username=jobhunter;Password=<senha>"
dotnet user-secrets set "Api:ApiKey" "<chave-longa-aleatória>"
cd ../..

dotnet build
dotnet ef database update --project src/JobHunter.Api
dotnet run --project src/JobHunter.Api
```

A API de revisão fica em `http://localhost:5180` (documentação interativa em `/scalar` no ambiente Development).

## Segredos e dados pessoais

Nada sensível é versionado:

- senhas, connection strings e chaves de API ficam em `dotnet user-secrets` ou no `.env` (ignorado pelo git);
- o banco roda num volume Docker, e backups (`backup/`, `*.dump`) são ignorados;
- currículo, sessões autenticadas e evidências de automação (`resume/`, `sessions/`, `Artifacts/`) ficam só na máquina local.

## Testes

```bash
dotnet test --filter-not-trait "Category=Live"
```

Os testes de integração sobem um PostgreSQL real via Testcontainers (Docker precisa estar rodando).

## Contribuindo

Ative o hook de mensagens de commit uma vez por clone:

```bash
git config core.hooksPath .githooks
```

Commits seguem Conventional Commits (`feat: add inhire job listing parser`). A documentação de arquitetura está em [`docs/`](docs/).
