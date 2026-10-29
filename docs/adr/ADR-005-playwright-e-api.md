# ADR-005: Playwright para conectores de navegador; HTTP quando houver API

## Status

Proposed

## Context

O InHire entrega páginas SPA. Alternativas: `HttpClient` + parsing de HTML (não vê conteúdo renderizado por JS), Selenium, Puppeteer-Sharp, Playwright. Outras plataformas futuras (Greenhouse, Lever) oferecem APIs públicas de vagas, onde navegador é desnecessário.

## Decision

- **Microsoft.Playwright** (Chromium) para conectores que precisam de navegador, através da infraestrutura compartilhada `JobHunter.Connectors.Browser` (um browser por processo, contexto por operação, evidências).
- Parsing separado da navegação: Playwright captura HTML/JSON; parsers puros (AngleSharp / System.Text.Json) são testados com fixtures.
- Conectores com API pública usam `IHttpClientFactory` + resiliência padrão e não referenciam o projeto Browser.

## Consequences

### Positive

- Playwright tem API .NET oficial, espera automática, seletores semânticos e interceptação de rede (útil para testes com fixtures).
- Parsers testáveis sem navegador.

### Negative

- Download do Chromium e consumo de memória.
- Mais lento que HTTP puro.

### Mitigations

- Concorrência de páginas limitada (padrão 1).
- Interceptar respostas JSON da própria página quando possível (mais estável que HTML).
