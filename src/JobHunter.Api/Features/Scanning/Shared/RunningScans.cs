using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Domain.Enums;

namespace JobHunter.Api.Features.Scanning.Shared;

internal static class RunningScans
{
    // A run still marked Running after this long was interrupted (the process stopped mid-scan) and no longer blocks the source.
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(1);

    public static IQueryable<Guid> SourceIds(AppDbContext db, DateTimeOffset now)
    {
        var since = now - StaleAfter;
        return db.ScanRuns
            .Where(run => run.Status == ScanRunStatus.Running && run.StartedAt > since)
            .Select(run => run.SourceTargetId);
    }
}
