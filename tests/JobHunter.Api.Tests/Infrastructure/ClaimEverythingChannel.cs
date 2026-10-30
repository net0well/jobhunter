using JobHunter.Connectors.Abstractions.Applications;
using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Api.Tests.Infrastructure;

internal sealed class ClaimEverythingChannel : IApplicationChannel
{
    private static readonly ConnectorError Unsupported = new(ConnectorErrorKind.Unsupported, "Stub.Unsupported", "Stub channel");

    public bool CanHandle(Uri applyUrl) => true;

    public Task<ConnectorResult<ApplicationForm>> InspectAsync(Uri applyUrl, CancellationToken ct) =>
        Task.FromResult(ConnectorResult<ApplicationForm>.Fail(Unsupported));

    public Task<ConnectorResult<SubmissionReceipt>> SubmitAsync(ApplicationSubmission submission, CancellationToken ct) =>
        Task.FromResult(ConnectorResult<SubmissionReceipt>.Fail(Unsupported));
}
