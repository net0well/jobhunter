using System.Diagnostics;
using JobHunter.Api.Domain;
using JobHunter.Api.Features.Matching.Shared;
using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Api.Tests.Features.Matching;

public sealed class MatchScorerTests
{
    private static readonly ScoringInput DotNetRemote = new(
        "Desenvolvedor .NET Pleno",
        "Atuação com C#, ASP.NET Core, PostgreSQL e Docker.",
        "Remoto",
        WorkModel.Remote,
        null,
        CompanyBlocked: false);

    [Fact]
    public void Score_ExcludedKeywordInTitle_DiscardsWithTheTerm()
    {
        var result = MatchScorer.Score(DotNetRemote with { Title = "Estágio em Desenvolvimento .NET" }, Profile(excluded: ["estágio"]));

        Assert.Equal("ExcludedKeyword:estágio", result.DiscardReason);
    }

    [Fact]
    public void Score_BlockedCompany_Discards()
    {
        var result = MatchScorer.Score(DotNetRemote with { CompanyBlocked = true }, Profile());

        Assert.Equal("CompanyBlocked", result.DiscardReason);
    }

    [Fact]
    public void Score_NoRequiredKeywordAnywhere_Discards()
    {
        var result = MatchScorer.Score(DotNetRemote, Profile(required: ["java"]));

        Assert.Equal("MissingRequired", result.DiscardReason);
    }

    [Fact]
    public void Score_CompatiblePosting_ScoresHighAndExplainsEachRule()
    {
        var profile = Profile(required: [".net"], preferred: ["c#", "postgresql", "docker"], workModels: [WorkModel.Remote], seniorities: ["pleno", "senior"], locations: ["São Paulo"]);

        var result = MatchScorer.Score(DotNetRemote, profile);

        Assert.Null(result.DiscardReason);
        Assert.Equal(100, result.Score);
        Assert.Equal(["Title", "Description", "Location", "WorkModel", "Seniority"], result.Reasons.Select(reason => reason.Rule));
        Assert.All(result.Reasons, reason => Assert.True(reason.Matched));
    }

    [Fact]
    public void Score_OnSiteInAnotherCity_DiscardsForLocation()
    {
        var posting = DotNetRemote with { Location = "Recife, PE", WorkModel = WorkModel.OnSite };

        var result = MatchScorer.Score(posting, Profile(locations: ["São Paulo"]));

        Assert.Equal("LocationMismatch", result.DiscardReason);
    }

    [Fact]
    public void Score_RemotePostingWhenProfileOnlyAcceptsOnSite_EarnsNoLocationOrWorkModelPoints()
    {
        var profile = Profile(required: [".net"], locations: ["São Paulo"], workModels: [WorkModel.OnSite]);

        var result = MatchScorer.Score(DotNetRemote, profile);

        Assert.Null(result.DiscardReason);
        Assert.False(result.Reasons.Single(reason => reason.Rule == "Location").Matched);
        Assert.False(result.Reasons.Single(reason => reason.Rule == "WorkModel").Matched);
    }

    [Fact]
    public void Score_PostingWithoutSeniorityOrLocation_LeavesThoseRulesOut()
    {
        var posting = new ScoringInput("Desenvolvedor .NET", "C# e PostgreSQL", null, null, null, false);
        var profile = Profile(required: [".net"], preferred: ["c#"], seniorities: ["senior"], locations: ["São Paulo"], workModels: [WorkModel.Remote]);

        var result = MatchScorer.Score(posting, profile);

        Assert.Equal(100, result.Score);
        Assert.DoesNotContain(result.Reasons, reason => reason.Rule is "Seniority" or "Location" or "WorkModel");
    }

    [Fact]
    public void Score_SomePreferredKeywords_EarnsPartialDescription()
    {
        var profile = Profile(preferred: ["c#", "kafka", "kubernetes", "redis"]);

        var result = MatchScorer.Score(DotNetRemote with { Title = "Backend Developer" }, profile);

        // Title misses (0 of 35) and one of three needed keywords hits the description (8.33 of 25): 8.33 / 60.
        Assert.Equal(14, result.Score);
    }

    [Fact]
    public void Score_ExcludedDescriptionKeywords_ArePenalizedUpToTheCap()
    {
        var posting = DotNetRemote with { DescriptionText = "Plantão, viagens frequentes, sobreaviso e hora extra." };
        var profile = Profile(required: [".net"], excludedDescription: ["plantão", "viagens", "sobreaviso", "hora extra"]);

        var result = MatchScorer.Score(posting, profile);

        Assert.Equal(70, result.Score);
        Assert.Equal(-30, result.Reasons.Where(reason => reason.Weight < 0).Sum(reason => reason.Weight));
    }

    [Fact]
    public void Score_ProfileWithoutCriteria_AcceptsEverything()
    {
        Assert.Equal(100, MatchScorer.Score(DotNetRemote, Profile()).Score);
    }

    [Fact]
    public void Best_SeveralProfiles_KeepsTheMatchingOne()
    {
        var strict = Profile(name: "Java", required: ["java"]);
        var dotnet = Profile(name: ".NET", required: [".net"]);

        var (result, profile) = ProfileMatcher.Best(DotNetRemote, [strict, dotnet]);

        Assert.Equal(".NET", profile.Name);
        Assert.Null(result.DiscardReason);
    }

    [Fact]
    public void Score_ThousandPostings_RunsWellUnderFiveSeconds()
    {
        var profile = Profile(required: [".net", "c#"], preferred: ["postgresql", "docker", "azure", "kafka"], excludedDescription: ["plantão"], locations: ["São Paulo", "Campinas"], workModels: [WorkModel.Remote, WorkModel.Hybrid], seniorities: ["pleno", "senior"]);
        var postings = Enumerable.Range(0, 1000)
            .Select(index => DotNetRemote with { Title = $"Desenvolvedor .NET {index}", DescriptionText = string.Concat(Enumerable.Repeat("C#, PostgreSQL e Docker em produção. ", 40)) })
            .ToList();

        var watch = Stopwatch.StartNew();
        foreach (var posting in postings)
        {
            MatchScorer.Score(posting, profile);
        }

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Scoring took {watch.Elapsed}");
    }

    private static SearchProfile Profile(
        string name = "Default",
        string[]? required = null,
        string[]? preferred = null,
        string[]? excluded = null,
        string[]? excludedDescription = null,
        string[]? locations = null,
        WorkModel[]? workModels = null,
        string[]? seniorities = null) =>
        SearchProfile.Create(
            new SearchProfileSettings(
                name,
                IsActive: true,
                required ?? [],
                preferred ?? [],
                excluded ?? [],
                excludedDescription ?? [],
                locations ?? [],
                workModels ?? [],
                seniorities ?? [],
                SearchProfile.DefaultMinScore,
                new ScoreWeights()),
            DateTimeOffset.UnixEpoch);
}
