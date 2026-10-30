using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Features.Postings.Shared;
using JobHunter.Connectors.Abstractions.Sources;
using Mediator;

namespace JobHunter.Api.Features.Postings;

internal static class RefreshPostingDetails
{
    internal sealed record Command(Guid Id) : IRequest<Result<PostingDetail>>;

    internal sealed class Handler(AppDbContext db, IConnectorRegistry registry, ConnectorInvoker invoker, PostingUpserter upserter, TimeProvider clock)
        : IRequestHandler<Command, Result<PostingDetail>>
    {
        public async ValueTask<Result<PostingDetail>> Handle(Command command, CancellationToken cancellationToken)
        {
            var posting = await db.FindWithDetailsAsync(command.Id, cancellationToken);
            if (posting is null)
                return PostingErrors.NotFound;

            IJobDetailsProvider? provider = null;
            var source = posting.Sources.FirstOrDefault(item =>
                registry.TryGet(item.ConnectorKey, out provider) && provider.CanHandle(new Uri(item.SourceUrl)));

            if (source is null)
                return Error.Conflict("Connector.CapabilityMissing", "No enabled connector can fetch details for this posting.");

            var details = await invoker.InvokeAsync(source.ConnectorKey, token => provider!.GetDetailsAsync(new Uri(source.SourceUrl), token), cancellationToken);
            if (!details.IsSuccess)
                return details.Error.ToError();

            if (posting.UpdateFrom(details.Value!, clock.GetUtcNow()))
                posting.AssignChannel(upserter.ResolveChannel(posting));

            await db.SaveChangesAsync(cancellationToken);
            return PostingDetail.From(posting);
        }
    }
}
