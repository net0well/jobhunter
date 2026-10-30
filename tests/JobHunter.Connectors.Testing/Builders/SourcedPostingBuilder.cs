using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.Testing.Fakes;

namespace JobHunter.Connectors.Testing.Builders;

public sealed class SourcedPostingBuilder
{
    private SourcedPosting _posting;

    private SourcedPostingBuilder(SourcedPosting posting) => _posting = posting;

    public static SourcedPostingBuilder Default(string tenant = "acme", string externalId = "backend-developer") =>
        new(new SourcedPosting(
            ExternalId: externalId,
            SourceUrl: FakeConnector.PostingUrl(tenant, externalId),
            Title: "Backend Developer",
            CompanyName: "Acme",
            Location: "São Paulo, SP",
            WorkModel: Abstractions.Sources.WorkModel.Remote,
            Seniority: "Mid",
            EmploymentType: "CLT",
            PublishedAt: null,
            DescriptionText: "Build and maintain .NET services.",
            ApplyUrl: FakeConnector.ApplyUrl(tenant, externalId),
            Extra: new Dictionary<string, string>()));

    public SourcedPostingBuilder WithExternalId(string externalId) => With(_posting with { ExternalId = externalId });

    public SourcedPostingBuilder WithTitle(string title) => With(_posting with { Title = title });

    public SourcedPostingBuilder WithCompany(string? companyName) => With(_posting with { CompanyName = companyName });

    public SourcedPostingBuilder WithLocation(string? location) => With(_posting with { Location = location });

    public SourcedPostingBuilder WithWorkModel(WorkModel? workModel) => With(_posting with { WorkModel = workModel });

    public SourcedPostingBuilder WithDescription(string? descriptionText) => With(_posting with { DescriptionText = descriptionText });

    public SourcedPostingBuilder WithPublishedAt(DateTimeOffset? publishedAt) => With(_posting with { PublishedAt = publishedAt });

    public SourcedPostingBuilder WithApplyUrl(Uri? applyUrl) => With(_posting with { ApplyUrl = applyUrl });

    public SourcedPosting Build() => _posting;

    private SourcedPostingBuilder With(SourcedPosting posting)
    {
        _posting = posting;
        return this;
    }
}
