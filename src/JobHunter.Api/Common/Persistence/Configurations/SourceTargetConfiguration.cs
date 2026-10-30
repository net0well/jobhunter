using JobHunter.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunter.Api.Common.Persistence.Configurations;

internal sealed class SourceTargetConfiguration : IEntityTypeConfiguration<SourceTarget>
{
    public void Configure(EntityTypeBuilder<SourceTarget> builder)
    {
        builder.HasKey(source => source.Id);
        builder.Property(source => source.Id).ValueGeneratedNever();

        builder.Property(source => source.ConnectorKey).HasMaxLength(64);
        builder.Property(source => source.TenantRef).HasMaxLength(200);
        builder.Property(source => source.DisplayName).HasMaxLength(SourceTarget.DisplayNameMaxLength);
        builder.Property(source => source.Checkpoint).HasMaxLength(2000);
        builder.Property(source => source.LastErrorCode).HasMaxLength(100);
        builder.Property(source => source.SearchCriteria).HasJsonConversion();
        builder.Property(source => source.Version).IsRowVersion();

        builder.HasOne<Company>().WithMany().HasForeignKey(source => source.CompanyId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(source => new { source.ConnectorKey, source.TenantRef }).IsUnique().HasFilter("kind = 'Tenant'");
        builder.HasIndex(source => new { source.IsActive, source.NextScanAt });
    }
}
