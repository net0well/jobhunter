using JobHunter.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunter.Api.Common.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(company => company.Id);
        builder.Property(company => company.Id).ValueGeneratedNever();

        builder.Property(company => company.Name).HasMaxLength(200);
        builder.Property(company => company.NormalizedName).HasMaxLength(200);
        builder.Property(company => company.Website).HasMaxLength(500);
        builder.Property(company => company.Notes).HasMaxLength(2000);

        builder.HasIndex(company => company.NormalizedName).IsUnique();
    }
}
