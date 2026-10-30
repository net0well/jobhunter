using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Features.Postings.Shared;
using Mediator;

namespace JobHunter.Api.Features.Postings;

internal static class RestorePosting
{
    internal sealed record Command(Guid Id) : IRequest<Result<PostingDetail>>;

    internal sealed class Handler(AppDbContext db) : IRequestHandler<Command, Result<PostingDetail>>
    {
        public async ValueTask<Result<PostingDetail>> Handle(Command command, CancellationToken cancellationToken)
        {
            var posting = await db.FindWithDetailsAsync(command.Id, cancellationToken);
            if (posting is null)
                return PostingErrors.NotFound;

            var restored = posting.Restore();
            if (restored.IsFailure)
                return restored.Error;

            await db.SaveChangesAsync(cancellationToken);
            return PostingDetail.From(posting);
        }
    }
}
