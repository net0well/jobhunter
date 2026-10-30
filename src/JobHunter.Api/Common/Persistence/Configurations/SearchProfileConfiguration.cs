using JobHunter.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunter.Api.Common.Persistence.Configurations;

internal sealed class SearchProfileConfiguration : IEntityTypeConfiguration<SearchProfile>
{
    public void Configure(EntityTypeBuilder<SearchProfile> builder)
    {
        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Name).HasMaxLength(100);
        builder.PrimitiveCollection(profile => profile.WorkModels).ElementType().HasConversion<string>();
        builder.Property(profile => profile.Weights).HasJsonConversion();
    }
}
