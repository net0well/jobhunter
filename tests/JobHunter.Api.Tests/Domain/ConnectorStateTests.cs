using JobHunter.Api.Common.Results;
using JobHunter.Api.Domain;

namespace JobHunter.Api.Tests.Domain;

public sealed class ConnectorStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 29);

    [Fact]
    public void Pause_FutureEnd_PausesUntilThen()
    {
        var state = new ConnectorState("fake", Today);

        var result = state.Pause(Now.AddHours(1), "Captcha detected", Now);

        Assert.True(result.IsSuccess);
        Assert.True(state.IsPaused(Now.AddMinutes(59)));
        Assert.False(state.IsPaused(Now.AddHours(1)));
        Assert.Equal("Captcha detected", state.PauseReason);
    }

    [Fact]
    public void Pause_EndInThePast_ReturnsValidationError()
    {
        var state = new ConnectorState("fake", Today);

        var result = state.Pause(Now.AddMinutes(-1), "Captcha detected", Now);

        Assert.Equal(ErrorType.Validation, result.Error?.Type);
        Assert.False(state.IsPaused(Now));
    }

    [Fact]
    public void Pause_LongReason_IsTruncatedToColumnLength()
    {
        var state = new ConnectorState("fake", Today);

        state.Pause(Now.AddHours(1), new string('x', ConnectorState.PauseReasonMaxLength + 50), Now);

        Assert.Equal(ConnectorState.PauseReasonMaxLength, state.PauseReason!.Length);
    }

    [Fact]
    public void Resume_PausedConnector_ClearsPause()
    {
        var state = new ConnectorState("fake", Today);
        state.Pause(Now.AddHours(1), "Layout changed", Now);

        state.Resume();

        Assert.False(state.IsPaused(Now));
        Assert.Null(state.PauseReason);
    }

    [Fact]
    public void RecordSuccessAndFailure_StoreTimestamps()
    {
        var state = new ConnectorState("fake", Today);

        state.RecordSuccess(Now);
        state.RecordFailure(Now.AddMinutes(5));

        Assert.Equal(Now, state.LastSuccessAt);
        Assert.Equal(Now.AddMinutes(5), state.LastFailureAt);
    }

    [Fact]
    public void RegisterSubmission_UnderLimit_IncrementsCounter()
    {
        var state = new ConnectorState("fake", Today);

        state.RegisterSubmission(Today, dailyLimit: 2);
        var result = state.RegisterSubmission(Today, dailyLimit: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, state.SubmissionsOn(Today));
    }

    [Fact]
    public void RegisterSubmission_LimitReached_ReturnsConflictWithoutIncrementing()
    {
        var state = new ConnectorState("fake", Today);
        state.RegisterSubmission(Today, dailyLimit: 1);

        var result = state.RegisterSubmission(Today, dailyLimit: 1);

        Assert.Equal("Application.DailyLimitReached", result.Error?.Code);
        Assert.Equal(1, state.SubmissionsOn(Today));
    }

    [Fact]
    public void RegisterSubmission_NextDay_StartsCountingAgain()
    {
        var state = new ConnectorState("fake", Today);
        state.RegisterSubmission(Today, dailyLimit: 1);

        var result = state.RegisterSubmission(Today.AddDays(1), dailyLimit: 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, state.SubmissionsOn(Today.AddDays(1)));
        Assert.Equal(0, state.SubmissionsOn(Today));
    }
}
