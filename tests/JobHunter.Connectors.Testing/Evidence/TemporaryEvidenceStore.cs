using System.Collections.Concurrent;
using JobHunter.Connectors.Abstractions.Evidence;

namespace JobHunter.Connectors.Testing.Evidence;

// Writes evidence to a throwaway folder so tests can check that the reported path really exists.
public sealed class TemporaryEvidenceStore : IConnectorEvidenceStore, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "jobhunter-evidence", Guid.NewGuid().ToString("N"));

    public ConcurrentQueue<string> Saved { get; } = new();

    public async Task<string> SaveAsync(string connectorKey, string name, ReadOnlyMemory<byte> content, CancellationToken ct)
    {
        var directory = Path.Combine(_root, connectorKey);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"{Saved.Count:D3}-{name}");
        await File.WriteAllBytesAsync(path, content.ToArray(), ct);
        Saved.Enqueue(path);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
