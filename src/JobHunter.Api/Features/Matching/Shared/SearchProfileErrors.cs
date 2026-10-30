using JobHunter.Api.Common.Results;

namespace JobHunter.Api.Features.Matching.Shared;

internal static class SearchProfileErrors
{
    public static readonly Error NotFound = Error.NotFound("SearchProfile.NotFound", "Search profile not found.");
}
