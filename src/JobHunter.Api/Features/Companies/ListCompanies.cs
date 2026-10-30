using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Text;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Features.Companies;

internal static class ListCompanies
{
    internal sealed record Query(bool? Blocked, string? Q) : IRequest<IReadOnlyList<Response>>;

    internal sealed record Response(Guid Id, string Name, bool IsBlocked, int Postings);

    internal sealed class Handler(AppDbContext db) : IRequestHandler<Query, IReadOnlyList<Response>>
    {
        public async ValueTask<IReadOnlyList<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var companies = db.Companies.AsNoTracking();

            if (query.Blocked is not null)
                companies = companies.Where(company => company.IsBlocked == query.Blocked);

            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var term = Normalizer.Text(query.Q);
                companies = companies.Where(company => company.NormalizedName.Contains(term));
            }

            return await companies
                .OrderBy(company => company.Name)
                .Select(company => new Response(company.Id, company.Name, company.IsBlocked, db.JobPostings.Count(posting => posting.CompanyId == company.Id)))
                .ToListAsync(cancellationToken);
        }
    }
}
