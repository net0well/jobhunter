using JobHunter.Api.Common.Results;

namespace JobHunter.Api.Domain;

internal sealed class ConnectorState
{
    public const int PauseReasonMaxLength = 500;

    public ConnectorState(string connectorKey, DateOnly submissionsDate)
    {
        ConnectorKey = connectorKey;
        SubmissionsDate = submissionsDate;
    }

    public string ConnectorKey { get; private set; }

    public DateTimeOffset? PausedUntil { get; private set; }

    public string? PauseReason { get; private set; }

    public DateTimeOffset? LastSuccessAt { get; private set; }

    public DateTimeOffset? LastFailureAt { get; private set; }

    public int SubmissionsToday { get; private set; }

    public DateOnly SubmissionsDate { get; private set; }

    public bool IsPaused(DateTimeOffset now) => PausedUntil > now;

    public Result Pause(DateTimeOffset until, string reason, DateTimeOffset now)
    {
        if (until <= now)
            return Error.Validation("Connector.InvalidPause", "A pause must end in the future.");

        if (string.IsNullOrWhiteSpace(reason))
            return Error.Validation("Connector.InvalidPause", "A pause needs a reason.");

        PausedUntil = until;
        PauseReason = reason.Length <= PauseReasonMaxLength ? reason : reason[..PauseReasonMaxLength];
        return Result.Success();
    }

    public void Resume()
    {
        PausedUntil = null;
        PauseReason = null;
    }

    public void RecordSuccess(DateTimeOffset now) => LastSuccessAt = now;

    public void RecordFailure(DateTimeOffset now) => LastFailureAt = now;

    public int SubmissionsOn(DateOnly today) => SubmissionsDate == today ? SubmissionsToday : 0;

    public void ResetDailyCounterIfNeeded(DateOnly today)
    {
        if (SubmissionsDate == today)
            return;

        SubmissionsDate = today;
        SubmissionsToday = 0;
    }

    public Result RegisterSubmission(DateOnly today, int dailyLimit)
    {
        ResetDailyCounterIfNeeded(today);

        if (SubmissionsToday >= dailyLimit)
            return Error.Conflict("Application.DailyLimitReached", $"Connector {ConnectorKey} reached its daily limit of {dailyLimit} submissions.");

        SubmissionsToday++;
        return Result.Success();
    }
}
