# 09 — Testes

Princípios: integração primeiro, PostgreSQL real via Testcontainers, sem `UseInMemoryDatabase`, AAA, nome `Metodo_Cenario_Resultado`, asserts nativos do xUnit (sem FluentAssertions 8+, que é comercial).

## 1. Pirâmide deste projeto

| Nível | Projeto | O que cobre | Ferramentas |
|---|---|---|---|
| Unidade (puro) | `JobHunter.Api.Tests/Domain`, partes de `Features/*/Shared` | Máquinas de estado, `MatchScorer`, `Normalizer`, `UrlNormalizer`, `FormAnswerer`, deduplicação | xUnit, `FakeTimeProvider` |
| Parsers de conector | `JobHunter.Connectors.InHire.Tests/Parsing` | HTML/JSON salvos → DTOs | xUnit, AngleSharp, Verify (snapshot) |
| Contrato de conector | `JobHunter.Connectors.InHire.Tests/Contract` | Conector cumpre os contratos | kit `JobHunter.Connectors.Testing`, Playwright com `FixtureRouter` |
| Integração | `JobHunter.Api.Tests/Features` | Endpoint → handler → PostgreSQL real, com `FakeConnector` | `WebApplicationFactory`, Testcontainers |
| Arquitetura | `JobHunter.Api.Tests/Architecture` | Regras de dependência entre núcleo e conectores | Reflexão |
| Live (manual) | `*/Live` | Conector contra a plataforma real, **somente leitura** | `[Trait("Category", "Live")]` |

Comando padrão (local e CI): `dotnet test --filter-not-trait "Category=Live"`.

## 2. Infraestrutura de integração

```csharp
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();
    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
```

`JobHunterApiFactory : WebApplicationFactory<Program>`:
- ambiente `Testing`;
- substitui a connection string pela do container e aplica migrations uma vez por coleção;
- desliga todos os workers (`Workers:*:Enabled = false`);
- remove conectores reais e registra `FakeConnector` com chave `fake` (todas as capacidades), configurável por teste;
- substitui `TimeProvider` por `FakeTimeProvider`;
- define `Api:ApiKey` de teste e um `HttpClient` com o cabeçalho.

Isolamento entre testes: limpar tabelas com `TRUNCATE ... CASCADE` antes de cada teste (ou Respawn, se preferir; verifique a licença).

Workers são testados chamando o **command** que eles enviam (ex.: `ScanDueSources`), não o `BackgroundService` em si. Um teste por worker verifica apenas que ele envia o command e sobrevive a exceções.

## 3. `FakeConnector`

```csharp
var fake = factory.Services.GetRequiredService<FakeConnector>();
fake.Tenants["acme"] = [ SourcedPostingBuilder.Default().WithTitle("Desenvolvedor .NET").Build() ];
fake.Forms[applyUrl] = ApplicationFormBuilder.Default().WithEmail().WithResume().WithCustomQuestion("Inglês?").Build();
fake.NextSubmitResult = ConnectorResult<SubmissionReceipt>.Fail(new(ConnectorErrorKind.CaptchaDetected, "Fake.Captcha", "captcha"));
```

Permite exercitar todo o pipeline (varredura → pontuação → preparação → aprovação → envio) sem rede.

## 4. Conectores: fixtures e contrato

- Fixtures reais (sem PII) em `Fixtures/`, uma por variação observada (vaga remota, híbrida, sem local, formulário com upload, com perguntas de múltipla escolha, página de captcha, página de vaga encerrada).
- `FixtureRouter` intercepta todas as requisições do Playwright e responde com os arquivos; requisições não mapeadas falham o teste (garante que nenhum teste toca a internet).
- `InHireContractTests` herda as classes do kit e fornece fixtures/URLs; o kit roda os mesmos testes para todos os conectores.
- Verify guarda o snapshot dos DTOs parseados: mudança de layout aparece como diff legível.

## 5. Testes de arquitetura

```csharp
[Fact]
public void Abstractions_ShouldNotReferenceInfrastructure()
{
    var forbidden = new[] { "Microsoft.Playwright", "AngleSharp", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore" };

    var referenced = typeof(IJobConnector).Assembly.GetReferencedAssemblies().Select(a => a.Name);

    Assert.Empty(referenced.Where(name => forbidden.Any(f => name!.StartsWith(f, StringComparison.Ordinal))));
}
```

Outros: conectores não referenciam `JobHunter.Api`; tipos em `JobHunter.Api.Features` e `JobHunter.Api.Workers` não têm campos, parâmetros ou retornos de tipos do namespace `JobHunter.Connectors.InHire` (verificado por reflexão sobre membros).

## 6. Casos obrigatórios (mínimo)

- Upsert idempotente: mesma listagem duas vezes não cria duplicatas.
- Deduplicação: mesma vaga por dois conectores fakes → uma `JobPosting`, duas `PostingSource`.
- Canal resolvido pela URL e `ManualApplyOnly` quando nenhum canal aceita.
- Vaga fechada após `ClosedAfterMissedScans` ausências.
- `Approve` falha com obrigatório sem resposta e com consentimento não revisado.
- Envio respeita limite diário por conector e global, janela de horário e conector pausado.
- `FormChanged` devolve para revisão sem enviar.
- `Captcha` pausa o conector e marca `RequiresManualAction`.
- Envio nunca é repetido automaticamente após erro.
- Registro de conectores falha no startup com chave duplicada ou capacidade sem implementação.
- Logs de um fluxo completo não contêm e-mail/telefone do perfil (teste com sink em memória).

## 7. TDD

Para cada critério de aceite: escreva o teste que falha, implemente o mínimo e refatore. Os nomes dos testes descrevem o comportamento (`ApproveApplication_RequiredFieldWithoutAnswer_ReturnsUnprocessable`).
