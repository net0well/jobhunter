using FluentValidation;
using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Domain;
using JobHunter.Api.Features.Sources.Shared;
using Mediator;

namespace JobHunter.Api.Features.Sources;

internal static class UpdateSource
{
    internal sealed record Request(string? DisplayName, TimeSpan? ScanInterval, bool? IsActive);

    internal sealed record Command(Guid Id, string? DisplayName, TimeSpan? ScanInterval, bool? IsActive) : IRequest<Result<SourceResponse>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(SourceTarget.DisplayNameMaxLength).When(command => command.DisplayName is not null);
            RuleFor(command => command.ScanInterval).InclusiveBetween(AddSource.MinScanInterval, AddSource.MaxScanInterval).When(command => command.ScanInterval is not null);
        }
    }

    internal sealed class Handler(AppDbContext db, IConnectorRegistry registry, TimeProvider clock) : IRequestHandler<Command, Result<SourceResponse>>
    {
        public async ValueTask<Result<SourceResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            var source = await db.SourceTargets.FindAsync([command.Id], cancellationToken);
            if (source is null)
                return Error.NotFound("Source.NotFound", "Source not found.");

            if (command.DisplayName is not null)
                source.Rename(command.DisplayName.Trim());

            if (command.ScanInterval is not null)
                source.ChangeScanInterval(command.ScanInterval);

            if (command.IsActive == true && !source.IsActive)
                source.Reactivate(clock.GetUtcNow());
            else if (command.IsActive == false)
                source.Deactivate();

            await db.SaveChangesAsync(cancellationToken);
            return SourceResponse.From(source, registry.Find(source.ConnectorKey) is not null);
        }
    }
}
