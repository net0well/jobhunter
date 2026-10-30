using JobHunter.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunter.Api.Common.Persistence.Configurations;

internal sealed class PostingSourceConfiguration : IEntityTypeConfiguration<PostingSource>
{
    public void Configure(EntityTypeBuilder<PostingSource> builder)
    {
        builder.HasKey(source => source.Id);
        builder.Property(source => source.Id).ValueGeneratedNever();

        builder.Property(source => source.ConnectorKey).HasMaxLength(64);
        builder.Property(source => source.ExternalId).HasMaxLength(300);
        builder.Property(source => source.SourceUrl).HasMaxLength(2000);
        builder.Property(source => source.Extra).HasJsonConversion();

        builder.HasOne<SourceTarget>().WithMany().HasForeignKey(source => source.SourceTargetId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(source => new { source.ConnectorKey, source.ExternalId }).IsUnique();
        builder.HasIndex(source => source.SourceTargetId);
    }
}
