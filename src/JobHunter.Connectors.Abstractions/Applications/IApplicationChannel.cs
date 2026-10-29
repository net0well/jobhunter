using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Connectors.Abstractions.Applications;

public interface IApplicationChannel
{
    bool CanHandle(Uri applyUrl);

    Task<ConnectorResult<ApplicationForm>> InspectAsync(Uri applyUrl, CancellationToken ct);

    Task<ConnectorResult<SubmissionReceipt>> SubmitAsync(ApplicationSubmission submission, CancellationToken ct);
}
