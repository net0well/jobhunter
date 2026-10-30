using System.Linq.Expressions;
using FluentValidation;
using JobHunter.Api.Domain;
using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Api.Features.Matching.Shared;

internal sealed record SearchProfileRequest(
    string Name,
    bool? IsActive,
    IReadOnlyList<string>? RequiredKeywords,
    IReadOnlyList<string>? PreferredKeywords,
    IReadOnlyList<string>? ExcludedKeywords,
    IReadOnlyList<string>? ExcludedDescriptionKeywords,
    IReadOnlyList<string>? Locations,
    IReadOnlyList<WorkModel>? WorkModels,
    IReadOnlyList<string>? Seniorities,
    int? MinScore,
    ScoreWeights? Weights)
{
    public SearchProfileSettings ToSettings() => new(
        Name,
        IsActive ?? true,
        RequiredKeywords ?? [],
        PreferredKeywords ?? [],
        ExcludedKeywords ?? [],
        ExcludedDescriptionKeywords ?? [],
        Locations ?? [],
        WorkModels ?? [],
        Seniorities ?? [],
        MinScore ?? SearchProfile.DefaultMinScore,
        Weights ?? new ScoreWeights());

    internal sealed class Validator : AbstractValidator<SearchProfileRequest>
    {
        private const int MaxTerms = 50;
        private const int MaxTermLength = 60;

        public Validator()
        {
            RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
            RuleFor(request => request.MinScore).InclusiveBetween(0, 100).When(request => request.MinScore is not null);

            Terms(request => request.RequiredKeywords);
            Terms(request => request.PreferredKeywords);
            Terms(request => request.ExcludedKeywords);
            Terms(request => request.ExcludedDescriptionKeywords);
            Terms(request => request.Locations);
            Terms(request => request.Seniorities);
            RuleForEach(request => request.WorkModels).IsInEnum();

            RuleFor(request => request.Weights!)
                .Must(weights => new[] { weights.Title, weights.Description, weights.Location, weights.WorkModel, weights.Seniority, weights.DescriptionPenalty }.All(weight => weight is >= 0 and <= 100))
                .WithMessage("Each weight must be between 0 and 100.")
                .When(request => request.Weights is not null);
        }

        private void Terms(Expression<Func<SearchProfileRequest, IEnumerable<string>?>> terms)
        {
            RuleFor(terms).Must(list => list is null || list.Count() <= MaxTerms).WithMessage($"At most {MaxTerms} terms.");
            RuleForEach(terms!).NotEmpty().MaximumLength(MaxTermLength);
        }
    }
}
