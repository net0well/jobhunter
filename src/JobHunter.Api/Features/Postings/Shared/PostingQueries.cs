using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Features.Postings.Shared;

internal static class PostingQueries
{
    public static Task<JobPosting?> FindWithDetailsAsync(this AppDbContext db, Guid id, CancellationToken ct) =>
        db.JobPostings
            .Include(posting => posting.Company)
            .Include(posting => posting.Sources)
            .FirstOrDefaultAsync(posting => posting.Id == id, ct);
}
