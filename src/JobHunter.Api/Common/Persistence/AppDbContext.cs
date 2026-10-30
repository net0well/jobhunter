using JobHunter.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Common.Persistence;

internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ConnectorState> ConnectorStates => Set<ConnectorState>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<SourceTarget> SourceTargets => Set<SourceTarget>();

    public DbSet<JobPosting> JobPostings => Set<JobPosting>();

    public DbSet<PostingSource> PostingSources => Set<PostingSource>();

    public DbSet<SearchProfile> SearchProfiles => Set<SearchProfile>();

    public DbSet<ScanRun> ScanRuns => Set<ScanRun>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(50);

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
