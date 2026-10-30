using JobHunter.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace JobHunter.Api.Common.Persistence.Interceptors;

internal sealed class TimestampsInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
            return;

        var now = clock.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<IHasCreatedAt>().Where(entry => entry.State == EntityState.Added))
        {
            entry.Property(entity => entity.CreatedAt).CurrentValue = now;
        }
    }
}
