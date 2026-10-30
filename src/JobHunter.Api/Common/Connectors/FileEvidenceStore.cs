using System.Globalization;
using JobHunter.Api.Common.Options;
using JobHunter.Connectors.Abstractions.Evidence;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Common.Connectors;

// Writes evidence under Artifacts/<connector>/<yyyy-MM-dd>/; the folder is ignored by git because it may hold personal data.
internal sealed class FileEvidenceStore(IOptions<ArtifactsOptions> options, IHostEnvironment environment, TimeProvider clock) : IConnectorEvidenceStore
{
    public async Task<string> SaveAsync(string connectorKey, string name, ReadOnlyMemory<byte> content, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var root = Path.GetFullPath(options.Value.RootPath, environment.ContentRootPath);
        var directory = Path.Combine(root, Sanitize(connectorKey), now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"{now.ToString("HHmmss-fff", CultureInfo.InvariantCulture)}-{Sanitize(name)}");
        await File.WriteAllBytesAsync(path, content.ToArray(), ct);
        return path;
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string([.. value.Select(character => invalid.Contains(character) ? '_' : character)]);
    }
}
