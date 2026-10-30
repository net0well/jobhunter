using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.Testing.Contracts;
using JobHunter.Connectors.Testing.Fakes;

namespace JobHunter.Api.Tests.Connectors.Fake;

public sealed class FakeErrorHandlingContractTests : ErrorHandlingContract
{
    protected override Task<ConnectorError?> RunAgainstUnexpectedLayoutAsync(CancellationToken ct) =>
        ListWithScriptedFailureAsync(new ConnectorError(ConnectorErrorKind.LayoutChanged, "Fake.LayoutChanged", "Unexpected page", EvidencePath: "Artifacts/fake/layout.html"), ct);

    protected override Task<ConnectorError?> RunAgainstCaptchaPageAsync(CancellationToken ct) =>
        ListWithScriptedFailureAsync(new ConnectorError(ConnectorErrorKind.CaptchaDetected, "Fake.Captcha", "Challenge page"), ct);

    private static async Task<ConnectorError?> ListWithScriptedFailureAsync(ConnectorError error, CancellationToken ct)
    {
        var fake = new FakeConnector(TimeProvider.System);
        fake.Tenants["acme"] = [];
        fake.FailNext(error);

        var result = await fake.ListAsync(new TenantRef("acme"), ct);
        return result.Error;
    }
}
