using JobHunter.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunter.Api.Common.Persistence.Configurations;

internal sealed class ConnectorStateConfiguration : IEntityTypeConfiguration<ConnectorState>
{
    public void Configure(EntityTypeBuilder<ConnectorState> builder)
    {
        builder.HasKey(state => state.ConnectorKey);

        builder.Property(state => state.ConnectorKey).HasMaxLength(64);
        builder.Property(state => state.PauseReason).HasMaxLength(ConnectorState.PauseReasonMaxLength);
    }
}
