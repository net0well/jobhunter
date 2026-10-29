# 11 — Infraestrutura local e CI

## 1. PostgreSQL com Docker Compose

`docker-compose.yml` na raiz:

```yaml
services:
  postgres:
    image: postgres:17-alpine
    container_name: jobhunter-postgres
    environment:
      POSTGRES_DB: jobhunter
      POSTGRES_USER: jobhunter
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?defina POSTGRES_PASSWORD no .env}
    ports:
      - "127.0.0.1:5432:5432"
    volumes:
      - jobhunter-pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U jobhunter -d jobhunter"]
      interval: 5s
      timeout: 3s
      retries: 10
    restart: unless-stopped

volumes:
  jobhunter-pgdata:
```

- Porta publicada só em `127.0.0.1`.
- Senha no arquivo `.env` (ignorado pelo git): `POSTGRES_PASSWORD=<senha>`. A mesma senha vai no user-secret da connection string.
- Use a mesma versão major de imagem no Compose e no `PostgresFixture` dos testes.

Opcional, para ver traces e métricas localmente: adicionar o Aspire Dashboard standalone como serviço e apontar `OpenTelemetry:OtlpEndpoint` para ele (confira a imagem e a porta atuais na documentação da Microsoft).

## 2. Primeiro uso

```bash
cp .env.example .env              # e defina POSTGRES_PASSWORD
docker compose up -d
dotnet build
pwsh src/JobHunter.Api/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet ef database update --project src/JobHunter.Api
dotnet run --project src/JobHunter.Api
```

Abra `http://localhost:5180/scalar`, informe a API key e:
1. `PUT /api/profile` com seus dados;
2. `POST /api/profile/resume` com o PDF;
3. `POST /api/search-profiles`;
4. `POST /api/sources` com `{ "connectorKey": "inhire", "kind": "Tenant", "input": "https://<empresa>.inhire.app/vagas" }`;
5. `POST /api/sources/{id}/scan` para testar na hora.

## 3. Backup

```bash
docker exec jobhunter-postgres pg_dump -U jobhunter -Fc jobhunter > backup/jobhunter-$(date +%F).dump
```

A pasta `backup/` também deve estar no `.gitignore`.

## 4. Rodar como serviço (opcional)

- **Windows**: `dotnet publish -c Release` e registrar com `sc.exe` (adicionar `UseWindowsService()` no host).
- **Linux**: unit do systemd (`UseSystemd()`).
- O navegador do Playwright precisa estar instalado para o usuário do serviço.

Containerizar a aplicação é possível (imagem base do Playwright para .NET), mas não é necessário para uso pessoal local.

## 5. CI no GitHub Actions

`.github/workflows/ci.yml`:

```yaml
name: ci

on:
  pull_request:
  push:
    branches: [main]

permissions:
  contents: read

jobs:
  commit-messages:
    if: github.event_name == 'pull_request'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - name: Validate commit messages
        run: |
          chmod +x .githooks/commit-msg
          for sha in $(git rev-list ${{ github.event.pull_request.base.sha }}..${{ github.event.pull_request.head.sha }}); do
            git log -1 --format=%B "$sha" > /tmp/msg
            .githooks/commit-msg /tmp/msg || { echo "Invalid message in $sha"; exit 1; }
          done

  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - run: dotnet restore
      - run: dotnet format --verify-no-changes --no-restore
      - run: dotnet build --no-restore -c Release
      - name: Install Playwright browsers
        run: pwsh src/JobHunter.Api/bin/Release/net10.0/playwright.ps1 install --with-deps chromium
      - run: dotnet test --no-build -c Release --filter-not-trait "Category=Live" --logger trx
```

- Testcontainers funciona nos runners `ubuntu-latest` (Docker disponível).
- O job `commit-messages` aplica no GitHub as mesmas regras do hook local (Conventional Commits, sem trailers de coautoria), então commits que escaparam do hook local são barrados no PR.
- Confira as versões atuais das actions (`actions/checkout`, `actions/setup-dotnet`) ao criar o arquivo.
- Na proteção da branch `main`, marque os checks `build-test` e `commit-messages` como obrigatórios.

## 6. Arquivos de raiz adicionais

`.env.example` (versionado):

```
POSTGRES_PASSWORD=
```
