using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using Mediator;

namespace JobHunter.Api.Features.Sources;

internal static class DeleteSource
{
    internal sealed record Command(Guid Id) : IRequest<Result>;

    // Postings found by the source stay; their PostingSource rows just lose the link to it.
    internal sealed class Handler(AppDbContext db) : IRequestHandler<Command, Result>
    {
        public async ValueTask<Result> Handle(Command command, CancellationToken cancellationToken)
        {
            var source = await db.SourceTargets.FindAsync([command.Id], cancellationToken);
            if (source is null)
                return Error.NotFound("Source.NotFound", "Source not found.");

            db.SourceTargets.Remove(source);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
