using JobHunter.Connectors.Abstractions.Applications;
using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Connectors.InHire.Applications;

// Registered now so InHire job URLs already resolve to this channel; reading and sending the form come with their own specs.
internal sealed class InHireApplicationChannel : IApplicationChannel
{
    private static readonly ConnectorError NotSupportedYet = new(
        ConnectorErrorKind.Unsupported,
        "InHire.ApplicationNotSupported",
        "Reading and submitting InHire application forms is not available yet.");

    public bool CanHandle(Uri applyUrl) => InHireUrls.TryParseJob(applyUrl, out _, out _);

    public Task<ConnectorResult<ApplicationForm>> InspectAsync(Uri applyUrl, CancellationToken ct) =>
        Task.FromResult(ConnectorResult<ApplicationForm>.Fail(NotSupportedYet));

    public Task<ConnectorResult<SubmissionReceipt>> SubmitAsync(ApplicationSubmission submission, CancellationToken ct) =>
        Task.FromResult(ConnectorResult<SubmissionReceipt>.Fail(NotSupportedYet));
}
