using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Common.Connectors;

internal sealed class ValidateConnectorOptions : IValidateOptions<ConnectorOptions>
{
    public ValidateOptionsResult Validate(string? name, ConnectorOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        List<string> failures = [.. results.Select(result => $"Connectors:{name}: {result.ErrorMessage}")];

        if (options.PolitenessDelayMin < TimeSpan.Zero || options.PolitenessDelayMin > options.PolitenessDelayMax)
            failures.Add($"Connectors:{name}: PolitenessDelayMin must be between zero and PolitenessDelayMax.");

        if (options.SubmissionDelayMin < TimeSpan.Zero || options.SubmissionDelayMin > options.SubmissionDelayMax)
            failures.Add($"Connectors:{name}: SubmissionDelayMin must be between zero and SubmissionDelayMax.");

        if (options.ReadTimeout <= TimeSpan.Zero || options.SubmitTimeout <= TimeSpan.Zero)
            failures.Add($"Connectors:{name}: ReadTimeout and SubmitTimeout must be positive.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
