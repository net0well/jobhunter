using System.Text.RegularExpressions;
using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.Testing.Builders;
using Xunit;

namespace JobHunter.Connectors.Testing.Contracts;

public abstract partial class JobDetailsProviderContract
{
    protected abstract string ConnectorKey { get; }

    protected abstract Uri OpenPostingUrl { get; }

    protected abstract Uri ClosedPostingUrl { get; }

    protected abstract IJobDetailsProvider CreateProvider();

    [Fact]
    public void CanHandle_OwnPostingUrls_ReturnsTrue()
    {
        var provider = CreateProvider();

        Assert.True(provider.CanHandle(OpenPostingUrl));
        Assert.True(provider.CanHandle(ClosedPostingUrl));
    }

    [Fact]
    public void CanHandle_OtherPlatformUrls_ReturnsFalse()
    {
        var provider = CreateProvider();

        var accepted = SampleUrls.ForeignTo(ConnectorKey).Where(provider.CanHandle);

        Assert.Empty(accepted);
    }

    [Fact]
    public async Task GetDetailsAsync_OpenPosting_ReturnsPlainTextDescription()
    {
        var result = await CreateProvider().GetDetailsAsync(OpenPostingUrl, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var details = result.Value!;
        Assert.True(details.IsOpen);
        Assert.False(string.IsNullOrWhiteSpace(details.ExternalId));
        Assert.False(string.IsNullOrWhiteSpace(details.Title));
        Assert.False(string.IsNullOrWhiteSpace(details.DescriptionText));
        Assert.DoesNotMatch(MarkupTag(), details.DescriptionText);
    }

    [Fact]
    public async Task GetDetailsAsync_ClosedPosting_ReturnsPostingClosed()
    {
        var result = await CreateProvider().GetDetailsAsync(ClosedPostingUrl, TestContext.Current.CancellationToken);

        Assert.Equal(ConnectorErrorKind.PostingClosed, result.Error?.Kind);
    }

    [GeneratedRegex("</?[a-zA-Z][a-zA-Z0-9]*[\\s/>]")]
    private static partial Regex MarkupTag();
}
