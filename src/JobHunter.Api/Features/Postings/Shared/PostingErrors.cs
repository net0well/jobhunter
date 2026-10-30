using JobHunter.Api.Common.Results;

namespace JobHunter.Api.Features.Postings.Shared;

internal static class PostingErrors
{
    public static readonly Error NotFound = Error.NotFound("Posting.NotFound", "Posting not found.");

    public static readonly Error AlreadyExists = Error.Conflict("Posting.AlreadyExists", "This posting is already registered.");
}
