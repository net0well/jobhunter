using FluentValidation;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Domain;
using JobHunter.Api.Domain.Enums;
using JobHunter.Api.Features.Sources.Shared;
using JobHunter.Connectors.Abstractions.Sources;
using Mediator;

namespace JobHunter.Api.Features.Sources;

internal static class AddSource
{
    public static readonly TimeSpan MinScanInterval = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan MaxScanInterval = TimeSpan.FromDays(7);

    internal sealed record SearchCriteriaRequest(
        IReadOnlyList<string> Keywords,
        string? Location,
        IReadOnlyList<WorkModel>? WorkModels,
        int? MaxResults);

    internal sealed record Command(
        string ConnectorKey,
        SourceKind Kind,
        string? Input,
        SearchCriteriaRequest? SearchCriteria,
        string? DisplayName,
        TimeSpan? ScanInterval) : IRequest<Result<SourceResponse>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.ConnectorKey).NotEmpty().MaximumLength(64);
            RuleFor(command => command.Kind).IsInEnum();
            RuleFor(command => command.Input).NotEmpty().MaximumLength(500).When(command => command.Kind == SourceKind.Tenant);
            RuleFor(command => command.SearchCriteria).NotNull().When(command => command.Kind == SourceKind.Search);
            RuleFor(command => command.SearchCriteria!.Keywords).NotEmpty().When(command => command.SearchCriteria is not null);
            RuleFor(command => command.SearchCriteria!.MaxResults).InclusiveBetween(1, 500).When(command => command.SearchCriteria?.MaxResults is not null);
            RuleFor(command => command.DisplayName).MaximumLength(SourceTarget.DisplayNameMaxLength);
            RuleFor(command => command.ScanInterval).InclusiveBetween(MinScanInterval, MaxScanInterval).When(command => command.ScanInterval is not null);
        }
    }

    internal sealed class Handler(AppDbContext db, SourceFactory factory) : IRequestHandler<Command, Result<SourceResponse>>
    {
        public async ValueTask<Result<SourceResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            var draft = new SourceDraft(command.ConnectorKey, command.Kind, command.Input, ToCriteria(command.SearchCriteria), command.DisplayName, command.ScanInterval);

            var created = await factory.CreateAsync(draft, cancellationToken);
            if (created.IsFailure)
                return created.Error;

            db.SourceTargets.Add(created.Value);
            await db.SaveChangesAsync(cancellationToken);

            return SourceResponse.From(created.Value, connectorEnabled: true);
        }

        private static SearchCriteria? ToCriteria(SearchCriteriaRequest? request) =>
            request is null ? null : new SearchCriteria(request.Keywords, request.Location, request.WorkModels ?? [], request.MaxResults ?? 50);
    }
}
