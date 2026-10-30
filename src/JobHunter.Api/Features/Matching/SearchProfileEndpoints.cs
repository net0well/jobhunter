using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Features.Matching.Shared;
using Mediator;

namespace JobHunter.Api.Features.Matching;

internal sealed class SearchProfileEndpoints : IEndpointGroup
{
    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        var profiles = api.MapGroup("/search-profiles").WithTags("Search profiles");

        profiles.MapGet("/", async (ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new ListSearchProfiles.Query(), ct)))
            .WithName("ListSearchProfiles")
            .WithSummary("Lists search profiles");

        profiles.MapPost("/", async (SearchProfileRequest request, ISender sender, CancellationToken ct) =>
            {
                var profile = await sender.Send(new CreateSearchProfile.Command(request), ct);
                return TypedResults.Created($"/api/search-profiles/{profile.Id}", profile);
            })
            .AddEndpointFilter<ValidationFilter<SearchProfileRequest>>()
            .WithName("CreateSearchProfile")
            .WithSummary("Creates a search profile used to score postings")
            .ProducesValidationProblem();

        profiles.MapPut("/{id:guid}", async (Guid id, SearchProfileRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateSearchProfile.Command(id, request), ct)).ToHttpResult(profile => TypedResults.Ok(profile)))
            .AddEndpointFilter<ValidationFilter<SearchProfileRequest>>()
            .WithName("UpdateSearchProfile")
            .WithSummary("Updates a search profile; run rescore to apply it to existing postings")
            .Produces<SearchProfileResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        profiles.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteSearchProfile.Command(id), ct)).ToHttpResult(() => TypedResults.NoContent()))
            .WithName("DeleteSearchProfile")
            .WithSummary("Removes a search profile")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        profiles.MapPost("/rescore", async (ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new RescoreAll.Command(), ct)))
            .WithName("RescoreAll")
            .WithSummary("Scores again every posting that is not in an application, closed or discarded by hand");
    }
}
