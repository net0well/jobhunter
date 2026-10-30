using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Connectors.InHire.Parsing;

internal static class InHireErrors
{
    public static ConnectorError LayoutChanged(string message) =>
        new(ConnectorErrorKind.LayoutChanged, "InHire.LayoutChanged", message);

    public static ConnectorError PostingClosed(string jobId) =>
        new(ConnectorErrorKind.PostingClosed, "InHire.JobClosed", $"Job {jobId} is no longer published.");
}
