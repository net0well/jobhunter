using FluentValidation;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Domain.Enums;
using JobHunter.Api.Features.Sources.Shared;
using Mediator;

namespace JobHunter.Api.Features.Sources;

internal static class ImportSources
{
    public const int MaxInputs = 200;

    internal sealed record Command(string ConnectorKey, IReadOnlyList<string> Inputs) : IRequest<Result<Response>>;

    internal sealed record InvalidInput(string Input, string Reason);

    internal sealed record Response(IReadOnlyList<SourceResponse> Created, IReadOnlyList<string> Duplicates, IReadOnlyList<InvalidInput> Invalid);

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.ConnectorKey).NotEmpty().MaximumLength(64);
            RuleFor(command => command.Inputs).NotEmpty().Must(inputs => inputs.Count <= MaxInputs).WithMessage($"At most {MaxInputs} inputs per import.");
        }
    }

    internal sealed class Handler(AppDbContext db, SourceFactory factory) : IRequestHandler<Command, Result<Response>>
    {
        public async ValueTask<Result<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            List<SourceResponse> created = [];
            List<string> duplicates = [];
            List<InvalidInput> invalid = [];
            HashSet<string> tenantsInBatch = [];

            foreach (var input in command.Inputs)
            {
                var result = await factory.CreateAsync(new SourceDraft(command.ConnectorKey, SourceKind.Tenant, input, null, null, null), cancellationToken);

                switch (result.Error?.Code)
                {
                    case null when tenantsInBatch.Add(result.Value.TenantRef!):
                        db.SourceTargets.Add(result.Value);
                        created.Add(SourceResponse.From(result.Value, connectorEnabled: true));
                        break;
                    case null or "Source.Duplicate":
                        duplicates.Add(input);
                        break;
                    case "Source.InvalidInput":
                        invalid.Add(new InvalidInput(input, result.Error!.Message));
                        break;
                    default:
                        return result.Error!;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            return new Response(created, duplicates, invalid);
        }
    }
}
