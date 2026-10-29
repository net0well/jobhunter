# ADR-004: Revisão humana obrigatória antes do envio

## Status

Proposed

## Context

Perguntas de triagem podem eliminar candidatos automaticamente; respostas genéricas prejudicam a candidatura. Termos de uso de plataformas podem restringir automação, e candidaturas em massa causam má impressão.

## Decision

Nenhuma candidatura é enviada sem `Application.Status == Approved`, e só o handler `SubmitApplication` chama `IApplicationChannel.SubmitAsync`. Não existe modo de aprovação automática. Consentimentos (`ConsentTerms`, `ConsentTalentPool`) nunca recebem proposta automática. Envio sem retry automático.

Modos disponíveis: `ReviewThenSubmit` (padrão) e `Assisted` (navegador visível, você clica em enviar).

## Consequences

### Positive

- Qualidade e controle sobre cada candidatura; risco legal e reputacional reduzido.

### Negative

- Exige sua atenção diária para revisar.

### Mitigations

- Preparação automática, banco de respostas reutilizáveis e notificações agregadas reduzem o tempo de revisão a minutos.
