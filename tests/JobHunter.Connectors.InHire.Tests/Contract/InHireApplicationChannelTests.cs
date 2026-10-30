using JobHunter.Connectors.Abstractions.Applications;
using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.InHire.Tests.Infrastructure;
using JobHunter.Connectors.Testing.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Connectors.InHire.Tests.Contract;

// Only URL recognition is in place; reading and sending forms belong to later work and must fail softly until then.
public sealed class InHireApplicationChannelTests : IDisposable
{
    private readonly InHireTestHost _host = new();

    private IApplicationChannel Channel => _host.Services.GetRequiredKeyedService<IApplicationChannel>(InHireConnectorRegistration.Key);

    [Fact]
    public void CanHandle_InHireJobUrls_ReturnsTrue()
    {
        Assert.True(Channel.CanHandle(InHireTestHost.JobUrl(InHireTestHost.OpenJobId)));
        Assert.True(Channel.CanHandle(SampleUrls.Postings[InHireConnectorRegistration.Key]));
    }

    [Fact]
    public void CanHandle_OtherPlatformUrls_ReturnsFalse()
    {
        Assert.DoesNotContain(SampleUrls.ForeignTo(InHireConnectorRegistration.Key), Channel.CanHandle);
    }

    [Fact]
    public async Task InspectAndSubmit_NotAvailableYet_ReturnUnsupportedWithoutCallingInHire()
    {
        var url = InHireTestHost.JobUrl(InHireTestHost.OpenJobId);

        var inspect = await Channel.InspectAsync(url, TestContext.Current.CancellationToken);
        var submit = await Channel.SubmitAsync(new ApplicationSubmission(url, "hash", [], Path.GetTempPath()), TestContext.Current.CancellationToken);

        Assert.Equal(ConnectorErrorKind.Unsupported, inspect.Error?.Kind);
        Assert.Equal(ConnectorErrorKind.Unsupported, submit.Error?.Kind);
        Assert.Empty(_host.Http.Requests);
    }

    public void Dispose() => _host.Dispose();
}
