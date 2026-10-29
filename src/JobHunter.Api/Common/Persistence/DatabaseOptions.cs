namespace JobHunter.Api.Common.Persistence;

internal sealed class DatabaseOptions
{
    public const string ConnectionStringName = "Default";

    public string? ConnectionString { get; set; }
}
