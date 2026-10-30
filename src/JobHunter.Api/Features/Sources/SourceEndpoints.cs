using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Features.Sources.Shared;
using Mediator;

namespace JobHunter.Api.Features.Sources;

internal sealed class SourceEndpoints : IEndpointGroup
{
    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        var sources = api.MapGroup("/sources").WithTags("Sources");

        sources.MapGet("/", async (string? connector, bool? active, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new ListSources.Query(connector, active), ct)))
            .WithName("ListSources")
            .WithSummary("Lists monitored sources");

        sources.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetSource.Query(id), ct)).ToHttpResult(source => TypedResults.Ok(source)))
            .WithName("GetSource")
            .WithSummary("Gets a source with its latest scan runs")
            .Produces<GetSource.Response>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        sources.MapPost("/", async (AddSource.Command command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttpResult(source => TypedResults.Created($"/api/sources/{source.Id}", source)))
            .AddEndpointFilter<ValidationFilter<AddSource.Command>>()
            .WithName("AddSource")
            .WithSummary("Registers a source to monitor")
            .Produces<SourceResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sources.MapPost("/import", async (ImportSources.Command command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttpResult(result => TypedResults.Ok(result)))
            .AddEndpointFilter<ValidationFilter<ImportSources.Command>>()
            .WithName("ImportSources")
            .WithSummary("Registers several tenant sources at once")
            .Produces<ImportSources.Response>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sources.MapPut("/{id:guid}", async (Guid id, UpdateSource.Request request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateSource.Command(id, request.DisplayName, request.ScanInterval, request.IsActive), ct))
                    .ToHttpResult(source => TypedResults.Ok(source)))
            .WithName("UpdateSource")
            .WithSummary("Renames, reschedules, pauses or reactivates a source")
            .Produces<SourceResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        sources.MapPost("/{id:guid}/scan", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new TriggerSourceScan.Command(id), ct)).ToHttpResult(run => TypedResults.Accepted((string?)null, run)))
            .WithName("TriggerSourceScan")
            .WithSummary("Queues an immediate scan of the source")
            .Produces<TriggerSourceScan.Response>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        sources.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteSource.Command(id), ct)).ToHttpResult(() => TypedResults.NoContent()))
            .WithName("DeleteSource")
            .WithSummary("Removes a source; its postings are kept")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
