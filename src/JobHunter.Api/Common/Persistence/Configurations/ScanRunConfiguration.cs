using JobHunter.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunter.Api.Common.Persistence.Configurations;

internal sealed class ScanRunConfiguration : IEntityTypeConfiguration<ScanRun>
{
    public void Configure(EntityTypeBuilder<ScanRun> builder)
    {
        builder.HasKey(run => run.Id);

        builder.Property(run => run.ConnectorKey).HasMaxLength(64);
        builder.Property(run => run.ErrorMessage).HasMaxLength(ScanRun.ErrorMessageMaxLength);
        builder.Property(run => run.EvidencePath).HasMaxLength(500);

        builder.HasOne<SourceTarget>().WithMany().HasForeignKey(run => run.SourceTargetId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(run => new { run.SourceTargetId, run.StartedAt });
        builder.HasIndex(run => run.StartedAt);
    }
}
