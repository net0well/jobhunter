using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Domain.Enums;
using JobHunter.Api.Features.Postings.Shared;
using Mediator;

namespace JobHunter.Api.Features.Postings;

internal sealed class PostingEndpoints : IEndpointGroup
{
    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        var postings = api.MapGroup("/postings").WithTags("Postings");

        postings.MapGet("/", async (JobPostingStatus? status, string? connector, int? minScore, string? q, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new ListPostings.Query(status, connector, minScore, q, page, pageSize), ct)))
            .WithName("ListPostings")
            .WithSummary("Lists postings by score, then by first seen")
            .Produces<PagedResponse<PostingSummary>>();

        postings.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetPosting.Query(id), ct)).ToHttpResult(posting => TypedResults.Ok(posting)))
            .WithName("GetPosting")
            .WithSummary("Gets a posting with its sources and score reasons")
            .Produces<PostingDetail>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        postings.MapPost("/import", async (ImportPostingFromUrl.Command command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttpResult(posting => TypedResults.Created($"/api/postings/{posting.Id}", posting)))
            .AddEndpointFilter<ValidationFilter<ImportPostingFromUrl.Command>>()
            .WithName("ImportPosting")
            .WithSummary("Imports a posting from its URL")
            .Produces<PostingDetail>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        postings.MapPost("/{id:guid}/discard", async (Guid id, DiscardPosting.Request request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DiscardPosting.Command(id, request.Reason), ct)).ToHttpResult(posting => TypedResults.Ok(posting)))
            .WithName("DiscardPosting")
            .WithSummary("Discards a posting; rescoring will not bring it back")
            .Produces<PostingDetail>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        postings.MapPost("/{id:guid}/restore", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RestorePosting.Command(id), ct)).ToHttpResult(posting => TypedResults.Ok(posting)))
            .WithName("RestorePosting")
            .WithSummary("Moves a discarded posting back to Matched")
            .Produces<PostingDetail>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        postings.MapPost("/{id:guid}/refresh", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RefreshPostingDetails.Command(id), ct)).ToHttpResult(posting => TypedResults.Ok(posting)))
            .WithName("RefreshPosting")
            .WithSummary("Fetches the posting details again from its connector")
            .Produces<PostingDetail>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
