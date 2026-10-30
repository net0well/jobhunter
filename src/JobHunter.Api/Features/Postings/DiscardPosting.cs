using FluentValidation;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Features.Postings.Shared;
using Mediator;

namespace JobHunter.Api.Features.Postings;

internal static class DiscardPosting
{
    internal sealed record Request(string Reason);

    internal sealed record Command(Guid Id, string Reason) : IRequest<Result<PostingDetail>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(command => command.Reason).NotEmpty().MaximumLength(500);
    }

    internal sealed class Handler(AppDbContext db) : IRequestHandler<Command, Result<PostingDetail>>
    {
        public async ValueTask<Result<PostingDetail>> Handle(Command command, CancellationToken cancellationToken)
        {
            var posting = await db.FindWithDetailsAsync(command.Id, cancellationToken);
            if (posting is null)
                return PostingErrors.NotFound;

            var discarded = posting.Discard(command.Reason.Trim());
            if (discarded.IsFailure)
                return discarded.Error;

            await db.SaveChangesAsync(cancellationToken);
            return PostingDetail.From(posting);
        }
    }
}
