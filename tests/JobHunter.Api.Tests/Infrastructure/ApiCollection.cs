namespace JobHunter.Api.Tests.Infrastructure;

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<JobHunterApiFactory>;
