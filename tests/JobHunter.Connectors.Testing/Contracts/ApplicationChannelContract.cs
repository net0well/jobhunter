using JobHunter.Connectors.Abstractions.Applications;
using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Testing.Builders;
using Xunit;

namespace JobHunter.Connectors.Testing.Contracts;

public abstract class ApplicationChannelContract
{
    protected abstract string ConnectorKey { get; }

    protected abstract Uri ApplyUrl { get; }

    protected abstract IApplicationChannel CreateChannel();

    // How many submissions reached the platform (or its fixture), to prove rejected submits had no effect.
    protected abstract int CountSubmissions();

    [Fact]
    public void CanHandle_OwnApplyUrl_ReturnsTrue()
    {
        Assert.True(CreateChannel().CanHandle(ApplyUrl));
    }

    [Fact]
    public void CanHandle_OtherPlatformUrls_ReturnsFalse()
    {
        var channel = CreateChannel();

        var accepted = SampleUrls.ForeignTo(ConnectorKey).Where(channel.CanHandle);

        Assert.Empty(accepted);
    }

    [Fact]
    public async Task InspectAsync_SameForm_ReturnsDeterministicHashMatchingFields()
    {
        var channel = CreateChannel();

        var first = await InspectAsync(channel);
        var second = await InspectAsync(channel);

        Assert.Equal(first.FormHash, second.FormHash);
        Assert.Equal(FormHasher.Compute(first.Fields), first.FormHash);
    }

    [Fact]
    public async Task InspectAsync_Form_HasUniqueNonBlankKeys()
    {
        var form = await InspectAsync(CreateChannel());

        Assert.All(form.Fields, field => Assert.False(string.IsNullOrWhiteSpace(field.Key)));
        Assert.Equal(form.Fields.Count, form.Fields.Select(field => field.Key).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task InspectAsync_Form_MarksRequiredFieldsAndRecognizesContactOrResume()
    {
        var form = await InspectAsync(CreateChannel());

        Assert.Contains(form.Fields, field => field.Required);
        Assert.Contains(form.Fields, field => field.Semantic is FieldSemantic.Email or FieldSemantic.Resume);
    }

    [Fact]
    public async Task InspectAsync_ChoiceFields_HaveOptions()
    {
        var form = await InspectAsync(CreateChannel());

        var choices = form.Fields.Where(field => field.Kind is FieldKind.SingleChoice or FieldKind.MultipleChoice);

        Assert.All(choices, field => Assert.NotEmpty(field.Options));
    }

    [Fact]
    public async Task SubmitAsync_StaleFormHash_ReturnsFormChangedWithoutSubmitting()
    {
        var channel = CreateChannel();
        var form = await InspectAsync(channel);
        var submittedBefore = CountSubmissions();

        var result = await channel.SubmitAsync(
            new ApplicationSubmission(ApplyUrl, "stale-" + form.FormHash, [], Path.GetTempPath()),
            TestContext.Current.CancellationToken);

        Assert.Equal(ConnectorErrorKind.FormChanged, result.Error?.Kind);
        Assert.Equal(submittedBefore, CountSubmissions());
    }

    [Fact]
    public async Task InspectAsync_OtherPlatformUrls_ReturnsErrorWithoutThrowing()
    {
        var channel = CreateChannel();

        foreach (var url in SampleUrls.ForeignTo(ConnectorKey))
        {
            var result = await channel.InspectAsync(url, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess, $"{url} should not be inspectable");
        }
    }

    [Fact]
    public async Task SubmitAsync_OtherPlatformUrls_ReturnsErrorWithoutThrowing()
    {
        var channel = CreateChannel();
        var submittedBefore = CountSubmissions();

        foreach (var url in SampleUrls.ForeignTo(ConnectorKey))
        {
            var result = await channel.SubmitAsync(new ApplicationSubmission(url, "any", [], Path.GetTempPath()), TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess, $"{url} should not be submittable");
        }

        Assert.Equal(submittedBefore, CountSubmissions());
    }

    private async Task<ApplicationForm> InspectAsync(IApplicationChannel channel)
    {
        var result = await channel.InspectAsync(ApplyUrl, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }
}
