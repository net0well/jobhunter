namespace JobHunter.Api.Domain;

internal sealed record ScoreWeights(
    int Title = 35,
    int Description = 25,
    int Location = 15,
    int WorkModel = 15,
    int Seniority = 10,
    int DescriptionPenalty = 10);
