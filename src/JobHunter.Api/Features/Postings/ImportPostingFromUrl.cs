using FluentValidation;
using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Common.Text;
using JobHunter.Api.Domain;
using JobHunter.Api.Features.Postings.Shared;
using JobHunter.Connectors.Abstractions.Sources;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Features.Postings;

internal static class ImportPostingFromUrl
{
    internal sealed record Command(Uri Url, string? Title, string? CompanyName) : IRequest<Result<PostingDetail>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Url)
                .Must(url => url.IsAbsoluteUri && url.Scheme is "http" or "https")
                .WithMessage("An absolute http(s) URL is required.");
            RuleFor(command => command.Title).MaximumLength(300);
            RuleFor(command => command.CompanyName).MaximumLength(200);
        }
    }

    // A connector that recognizes the URL supplies the details; otherwise the posting is kept with what the user typed.
    internal sealed class Handler(AppDbContext db, IConnectorRegistry registry, ConnectorInvoker invoker, PostingUpserter upserter, TimeProvider clock)
        : IRequestHandler<Command, Result<PostingDetail>>
    {
        public async ValueTask<Result<PostingDetail>> Handle(Command command, CancellationToken cancellationToken)
        {
            var normalized = UrlNormalizer.Normalize(command.Url)!;
            var url = command.Url.ToString();

            if (await db.JobPostings.AnyAsync(posting => posting.NormalizedApplyUrl == normalized, cancellationToken)
                || await db.PostingSources.AnyAsync(source => source.SourceUrl == url, cancellationToken))
                return PostingErrors.AlreadyExists;

            IJobDetailsProvider? provider = null;
            var connector = registry.Connectors.FirstOrDefault(descriptor =>
                registry.TryGet(descriptor.Key, out provider) && provider.CanHandle(command.Url));

            var imported = connector is null
                ? await ImportManuallyAsync(command, normalized, cancellationToken)
                : await ImportWithConnectorAsync(command, connector.Key, provider!, cancellationToken);

            if (imported.IsFailure)
                return imported.Error;

            await db.SaveChangesAsync(cancellationToken);

            var postingId = await db.PostingSources
                .Where(source => source.ConnectorKey == imported.Value.ConnectorKey && source.ExternalId == imported.Value.ExternalId)
                .Select(source => source.JobPostingId)
                .SingleAsync(cancellationToken);

            return PostingDetail.From((await db.FindWithDetailsAsync(postingId, cancellationToken))!);
        }

        private async Task<Result<(string ConnectorKey, string ExternalId)>> ImportWithConnectorAsync(
            Command command,
            string connectorKey,
            IJobDetailsProvider provider,
            CancellationToken ct)
        {
            var details = await invoker.InvokeAsync(connectorKey, token => provider.GetDetailsAsync(command.Url, token), ct);
            if (!details.IsSuccess)
                return details.Error.ToError();

            var found = details.Value!;
            if (await db.PostingSources.AnyAsync(source => source.ConnectorKey == connectorKey && source.ExternalId == found.ExternalId, ct))
                return PostingErrors.AlreadyExists;

            var item = new SourcedPosting(
                found.ExternalId,
                command.Url,
                found.Title,
                command.CompanyName,
                Location: null,
                WorkModel: null,
                Seniority: null,
                EmploymentType: null,
                PublishedAt: null,
                found.DescriptionText,
                found.ApplyUrl ?? command.Url,
                found.Extra);

            var outcome = await upserter.UpsertAsync(connectorKey, null, command.CompanyName ?? command.Url.Host, [item], ct);

            foreach (var created in outcome.CreatedPostings)
            {
                created.Posting.UpdateFrom(found, clock.GetUtcNow());
            }

            return (connectorKey, found.ExternalId);
        }

        private async Task<Result<(string ConnectorKey, string ExternalId)>> ImportManuallyAsync(Command command, string normalizedUrl, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(command.Title))
                return Error.Validation("Posting.TitleRequired", "No connector recognizes this URL, so a title is required.");

            var item = new SourcedPosting(
                normalizedUrl,
                command.Url,
                command.Title.Trim(),
                command.CompanyName,
                Location: null,
                WorkModel: null,
                Seniority: null,
                EmploymentType: null,
                PublishedAt: null,
                DescriptionText: null,
                command.Url,
                new Dictionary<string, string>());

            await upserter.UpsertAsync(PostingSource.ManualConnectorKey, null, command.CompanyName ?? command.Url.Host, [item], ct);
            return (PostingSource.ManualConnectorKey, normalizedUrl);
        }
    }
}
