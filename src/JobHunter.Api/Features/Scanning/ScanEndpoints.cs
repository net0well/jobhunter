using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Domain.Enums;
using JobHunter.Api.Features.Scanning.Shared;
using Mediator;

namespace JobHunter.Api.Features.Scanning;

internal sealed class ScanEndpoints : IEndpointGroup
{
    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        api.MapGet("/scan-runs", async (Guid? sourceId, string? connector, ScanRunStatus? status, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new ListScanRuns.Query(sourceId, connector, status, page, pageSize), ct)))
            .WithTags("Scanning")
            .WithName("ListScanRuns")
            .WithSummary("Lists scan runs, most recent first")
            .Produces<PagedResponse<ScanRunResponse>>();
    }
}
