using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Features.Companies;

internal static class SetCompanyBlocked
{
    internal sealed record Command(Guid Id, bool Blocked) : IRequest<Result<Response>>;

    internal sealed record Response(Guid Id, string Name, bool IsBlocked, int PostingsToRescore);

    // Blocking does not discard anything directly: the company's open postings go back to scoring, which applies the rule.
    internal sealed class Handler(AppDbContext db) : IRequestHandler<Command, Result<Response>>
    {
        public async ValueTask<Result<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var company = await db.Companies.FindAsync([command.Id], cancellationToken);
            if (company is null)
                return Error.NotFound("Company.NotFound", "Company not found.");

            if (command.Blocked)
                company.Block();
            else
                company.Unblock();

            var postings = await db.JobPostings.Where(posting => posting.CompanyId == company.Id).ToListAsync(cancellationToken);
            var rescored = postings.Count(posting => posting.RequestRescore());

            await db.SaveChangesAsync(cancellationToken);
            return new Response(company.Id, company.Name, company.IsBlocked, rescored);
        }
    }
}
