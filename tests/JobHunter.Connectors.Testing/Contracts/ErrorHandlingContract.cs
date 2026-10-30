using JobHunter.Connectors.Abstractions.Results;
using Xunit;

namespace JobHunter.Connectors.Testing.Contracts;

public abstract class ErrorHandlingContract
{
    // Runs any read operation against a page or payload whose structure the connector does not recognize.
    protected abstract Task<ConnectorError?> RunAgainstUnexpectedLayoutAsync(CancellationToken ct);

    // Runs any read operation against a page that shows an anti-bot challenge.
    protected abstract Task<ConnectorError?> RunAgainstCaptchaPageAsync(CancellationToken ct);

    [Fact]
    public async Task UnexpectedLayout_ReturnsLayoutChangedWithEvidence()
    {
        var error = await RunAgainstUnexpectedLayoutAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(error);
        Assert.Equal(ConnectorErrorKind.LayoutChanged, error.Kind);
        Assert.False(string.IsNullOrWhiteSpace(error.EvidencePath), "LayoutChanged must point to the saved evidence");
    }

    [Fact]
    public async Task CaptchaPage_ReturnsCaptchaDetected()
    {
        var error = await RunAgainstCaptchaPageAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(error);
        Assert.Equal(ConnectorErrorKind.CaptchaDetected, error.Kind);
    }
}
