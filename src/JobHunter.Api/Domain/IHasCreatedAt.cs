namespace JobHunter.Api.Domain;

internal interface IHasCreatedAt
{
    DateTimeOffset CreatedAt { get; }
}
