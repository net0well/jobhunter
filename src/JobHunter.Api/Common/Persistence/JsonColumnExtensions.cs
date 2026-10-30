using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunter.Api.Common.Persistence;

internal static class JsonColumnExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    // Stored as jsonb through a string conversion; the comparer makes EF detect changes inside collections and records.
    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> property)
    {
        property
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, Options),
                json => JsonSerializer.Deserialize<T>(json, Options)!,
                new ValueComparer<T>(
                    (left, right) => JsonSerializer.Serialize(left, Options) == JsonSerializer.Serialize(right, Options),
                    value => JsonSerializer.Serialize(value, Options).GetHashCode(StringComparison.Ordinal),
                    value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!));

        return property;
    }
}
