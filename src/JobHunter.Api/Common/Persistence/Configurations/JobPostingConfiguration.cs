using JobHunter.Api.Domain;
using JobHunter.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunter.Api.Common.Persistence.Configurations;

internal sealed class JobPostingConfiguration : IEntityTypeConfiguration<JobPosting>
{
    public void Configure(EntityTypeBuilder<JobPosting> builder)
    {
        builder.HasKey(posting => posting.Id);

        builder.Property(posting => posting.Title).HasMaxLength(300);
        builder.Property(posting => posting.NormalizedTitle).HasMaxLength(300);
        builder.Property(posting => posting.Location).HasMaxLength(300);
        builder.Property(posting => posting.NormalizedCity).HasMaxLength(200);
        builder.Property(posting => posting.Seniority).HasMaxLength(100);
        builder.Property(posting => posting.EmploymentType).HasMaxLength(100);
        builder.Property(posting => posting.ApplyUrl).HasMaxLength(2000);
        builder.Property(posting => posting.NormalizedApplyUrl).HasMaxLength(2000);
        builder.Property(posting => posting.ApplyChannelKey).HasMaxLength(64);
        builder.Property(posting => posting.Fingerprint).HasMaxLength(64);
        builder.Property(posting => posting.DiscardReason).HasMaxLength(500);
        builder.Property(posting => posting.MatchReasons).HasJsonConversion();
        builder.Property(posting => posting.Version).IsRowVersion();

        builder.HasOne(posting => posting.Company).WithMany().HasForeignKey(posting => posting.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(posting => posting.Sources).WithOne().HasForeignKey(source => source.JobPostingId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(posting => posting.Sources).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(posting => posting.Status);
        builder.HasIndex(posting => posting.CompanyId);
        builder.HasIndex(posting => new { posting.Fingerprint, posting.FirstSeenAt });
        builder.HasIndex(posting => posting.NormalizedApplyUrl).IsUnique().HasFilter("normalized_apply_url IS NOT NULL");
        builder.HasIndex(posting => posting.MatchScore).IsDescending().HasFilter($"status = '{JobPostingStatus.Matched}'");
    }
}
