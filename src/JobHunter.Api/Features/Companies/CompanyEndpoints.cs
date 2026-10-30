using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Common.Results;
using Mediator;

namespace JobHunter.Api.Features.Companies;

internal sealed class CompanyEndpoints : IEndpointGroup
{
    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        var companies = api.MapGroup("/companies").WithTags("Companies");

        companies.MapGet("/", async (bool? blocked, string? q, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new ListCompanies.Query(blocked, q), ct)))
            .WithName("ListCompanies")
            .WithSummary("Lists companies found in postings");

        companies.MapPost("/{id:guid}/block", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SetCompanyBlocked.Command(id, Blocked: true), ct)).ToHttpResult(company => TypedResults.Ok(company)))
            .WithName("BlockCompany")
            .WithSummary("Blocks a company; its postings are discarded on the next scoring")
            .Produces<SetCompanyBlocked.Response>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        companies.MapPost("/{id:guid}/unblock", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SetCompanyBlocked.Command(id, Blocked: false), ct)).ToHttpResult(company => TypedResults.Ok(company)))
            .WithName("UnblockCompany")
            .WithSummary("Unblocks a company; its postings are scored again")
            .Produces<SetCompanyBlocked.Response>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
