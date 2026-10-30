using JobHunter.Api.Common.Endpoints;
using Mediator;

namespace JobHunter.Api.Features.Connectors;

internal sealed class ConnectorEndpoints : IEndpointGroup
{
    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        var connectors = api.MapGroup("/connectors").WithTags("Connectors");

        connectors.MapGet("/", async (ISender sender, CancellationToken ct) => TypedResults.Ok(await sender.Send(new ListConnectors.Query(), ct)))
            .WithName("ListConnectors")
            .WithSummary("Lists enabled connectors with their capabilities and operational state")
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
