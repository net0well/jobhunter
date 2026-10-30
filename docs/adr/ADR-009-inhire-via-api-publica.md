# ADR-009: Conector InHire via API pública da página de carreiras

## Status

Accepted (2026-10-30)

## Context

As páginas `https://{slug}.inhire.app/vagas` são uma SPA. O reconhecimento de 2026-10-30 (`docs/07.1-conector-inhire.md` §3) mostrou que a própria página obtém os dados de endpoints públicos em `https://api.inhire.app`, sem login nem token:

- `GET /tenants/public/config/resolve/{slug}` — dados do tenant (404 `Subdomain not found` quando não existe);
- `GET /job-posts/public/pages` com cabeçalho `X-Tenant: {slug}` — todas as vagas publicadas, sem paginação (52 vagas numa resposta no maior tenant observado);
- `GET /job-posts/public/pages/{jobId}` — detalhes, incluindo descrição em HTML, tipo de contratação e campos do formulário (404 `Job page not found` quando a vaga não existe mais).

As respostas são as mesmas com um User-Agent próprio e só o `X-Tenant`; não é preciso se identificar como o front-end do InHire. O `robots.txt` libera `User-agent: *` em `/`. Os termos de uso não foram encontrados: o próprio tenant aponta `termsOfUseUrl` para a política de privacidade, que não trata de acesso automatizado.

A alternativa prevista (Playwright interceptando esse mesmo JSON) obtém exatamente os mesmos dados, com o custo de um navegador por processo.

## Decision

- O conector InHire lê vagas e detalhes por HTTP (`IHttpClientFactory`), nos endpoints públicos acima, e **não** usa o projeto `JobHunter.Connectors.Browser` para captura.
- Identificação honesta: `User-Agent: JobHunter/<versão> (+https://github.com/net0well/jobhunter)` e apenas o cabeçalho `X-Tenant`. Sem `X-Inhire-Client`, sem `Origin` ou cookies forjados.
- Cortesia e limites continuam no núcleo (`ConnectorInvoker`): 1 requisição por vez, 4–10 s entre chamadas, 10 por minuto.
- O JSON é validado ao ser lido: campo obrigatório ausente ou tipo inesperado vira `LayoutChanged`, com o corpo salvo como evidência.
- Respostas de desafio anti-bot (página HTML da Cloudflare em vez de JSON, cabeçalho `cf-mitigated`) viram `CaptchaDetected` com evidência; nada tenta resolvê-las.
- Envio de candidaturas fica **fora** desta decisão. O endpoint de envio aceita reCAPTCHA (`X-Recaptcha`), e a forma de enviar será decidida na spec 005, com os termos de uso verificados antes.

## Consequences

### Positive

- Sem Chromium para capturar: mais rápido, menos memória, CI sem instalação de navegador.
- Os parsers trabalham sobre o mesmo JSON que o Playwright interceptaria; trocar de estratégia não muda os parsers.
- Fixtures são arquivos JSON simples, fáceis de anonimizar.

### Negative

- A API não é documentada como pública para terceiros e pode mudar sem aviso.
- Sem termos de uso encontrados, a permissão de acesso automatizado não está declarada explicitamente.

### Mitigations

- Mudança de formato é tratada como `LayoutChanged` (pausa o conector e notifica), coberta por testes com fixtures.
- Volume mínimo e cortesia fixa; somente leitura de vagas públicas.
- Se o InHire publicar termos que proíbam esse acesso, ou passar a exigir desafio na listagem, o conector é desabilitado e este ADR é revisto (alternativa: captura assistida ou por alertas).
