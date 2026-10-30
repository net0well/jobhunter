using System.Threading.Channels;

namespace JobHunter.Api.Features.Scanning.Shared;

// Scans requested through the API run in the background so the request returns immediately with the run id.
internal sealed class ScanRequestQueue
{
    public const int Capacity = 100;

    private readonly Channel<ScanRequest> _requests = Channel.CreateBounded<ScanRequest>(new BoundedChannelOptions(Capacity)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait,
    });

    public bool TryEnqueue(ScanRequest request) => _requests.Writer.TryWrite(request);

    public bool TryDequeue(out ScanRequest? request) => _requests.Reader.TryRead(out request);

    public IAsyncEnumerable<ScanRequest> ReadAllAsync(CancellationToken ct) => _requests.Reader.ReadAllAsync(ct);
}
