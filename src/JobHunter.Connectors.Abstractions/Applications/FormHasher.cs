using System.Security.Cryptography;
using System.Text.Json;

namespace JobHunter.Connectors.Abstractions.Applications;

public static class FormHasher
{
    // Only what changes the meaning of a submission takes part in the hash: labels and help texts
    // are cosmetic, and field/option order on the page must not invalidate a reviewed form.
    public static string Compute(IEnumerable<FormField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartArray();

            foreach (var field in fields.OrderBy(field => field.Key, StringComparer.Ordinal))
            {
                json.WriteStartObject();
                json.WriteString("key", field.Key);
                json.WriteString("kind", field.Kind.ToString());
                json.WriteBoolean("required", field.Required);

                json.WriteStartArray("options");
                foreach (var option in field.Options.Select(option => option.Value).Order(StringComparer.Ordinal))
                {
                    json.WriteStringValue(option);
                }

                json.WriteEndArray();
                json.WriteEndObject();
            }

            json.WriteEndArray();
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }
}
