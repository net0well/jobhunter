using System.ComponentModel.DataAnnotations;

namespace JobHunter.Api.Common.Connectors;

internal sealed class ConnectorOptions
{
    public const string Section = "Connectors";

    public bool Enabled { get; set; }

    public TimeSpan? ScanInterval { get; set; }

    [Range(1, 10)]
    public int MaxConcurrency { get; set; } = 1;

    [Range(1, 600)]
    public int RequestsPerMinute { get; set; } = 10;

    public TimeSpan PolitenessDelayMin { get; set; } = TimeSpan.FromSeconds(3);

    public TimeSpan PolitenessDelayMax { get; set; } = TimeSpan.FromSeconds(8);

    public TimeSpan SubmissionDelayMin { get; set; } = TimeSpan.FromMinutes(2);

    public TimeSpan SubmissionDelayMax { get; set; } = TimeSpan.FromMinutes(5);

    [Range(0, 50)]
    public int DailySubmissionLimit { get; set; } = 5;

    [Range(1, 10)]
    public int ClosedAfterMissedScans { get; set; } = 2;

    public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromMinutes(1);

    public TimeSpan SubmitTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
