# ADR-007: Conectores plugáveis por capacidade com composição estática

## Status

Proposed

## Context

Hoje só existe o InHire, mas o usuário quer adicionar LinkedIn, Gupy, Greenhouse e outras plataformas no futuro sem reescrever o sistema. As plataformas diferem em quase tudo: página por empresa vs. busca central vs. API pública; com ou sem login; com ou sem envio de candidatura; navegador vs. HTTP; termos de uso diferentes.

Alternativas:
- **Interface única `IJobBoard`** com todos os métodos: força `NotSupportedException` e `if (plataforma == ...)` no núcleo.
- **Plugins dinâmicos** (`AssemblyLoadContext`, DLLs carregadas em runtime): flexível, mas complica build, testes, versionamento e debugging para um projeto pessoal.
- **Contratos por capacidade + projetos separados + composição estática** com keyed services.

## Decision

- Projeto `JobHunter.Connectors.Abstractions` com interfaces pequenas por capacidade (`ITenantJobSource`, `ISearchJobSource`, `IFeedJobSource`, `IJobDetailsProvider`, `IApplicationChannel`, `IAssistedApplicationChannel`, `ITenantDiscovery`, `IConnectorHealthProbe`), DTOs neutros, `ConnectorDescriptor` com flags de capacidade e `ConnectorResult`/`ConnectorError` próprios.
- Um projeto por plataforma (`JobHunter.Connectors.<Plataforma>`), expondo apenas `Add<Plataforma>Connector(...)`.
- Registro como **keyed services** pela chave do conector; `IConnectorRegistry` resolve capacidades; validação de consistência no startup.
- Composição estática: o host referencia os projetos de conector e chama os métodos de registro em um único arquivo (`ConnectorRegistration.cs`). Habilitação por configuração.
- Preocupações transversais (rate limit, cortesia, resiliência, métricas, tradução de exceções) ficam no núcleo, no `ConnectorInvoker`.
- Kit de testes de contrato compartilhado (`JobHunter.Connectors.Testing`) obrigatório para todo conector.
- Infraestrutura de navegador compartilhada num projeto separado (`JobHunter.Connectors.Browser`), usada só por conectores que precisam dela.

## Consequences

### Positive

- Adicionar plataforma = novo projeto + uma linha de registro + configuração; núcleo intacto.
- Conectores pequenos e testáveis; falhas isoladas por conector.
- Termos de uso refletidos nas capacidades implementadas (ex.: LinkedIn sem canal de envio).

### Negative

- Mais projetos na solução.
- Mudanças quebrantes no contrato exigem atualizar todos os conectores.

### Mitigations

- Regras de versionamento do contrato em `docs/07-conectores.md` §9 (preferir capacidades novas e campos opcionais).
- Testes de arquitetura garantem que o núcleo não depende de conectores concretos.
