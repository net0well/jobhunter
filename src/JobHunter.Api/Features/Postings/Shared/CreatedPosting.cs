using JobHunter.Api.Domain;

namespace JobHunter.Api.Features.Postings.Shared;

internal sealed record CreatedPosting(JobPosting Posting, Uri SourceUrl);
