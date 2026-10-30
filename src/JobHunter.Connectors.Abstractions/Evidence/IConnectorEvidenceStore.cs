namespace JobHunter.Connectors.Abstractions.Evidence;

// Connectors hand over what they saw when something went wrong; the host decides where it is kept and for how long.
public interface IConnectorEvidenceStore
{
    // Returns the path of the saved file, to be reported in ConnectorError.EvidencePath.
    Task<string> SaveAsync(string connectorKey, string name, ReadOnlyMemory<byte> content, CancellationToken ct);
}
