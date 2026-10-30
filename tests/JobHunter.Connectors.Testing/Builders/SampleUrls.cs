namespace JobHunter.Connectors.Testing.Builders;

public static class SampleUrls
{
    public static IReadOnlyDictionary<string, Uri> Postings { get; } = new Dictionary<string, Uri>
    {
        ["inhire"] = new("https://acme.inhire.app/vagas/5f8b856e-f4a1-486c-94c2-385c18e483e7/desenvolvedor-net"),
        ["gupy"] = new("https://acme.gupy.io/jobs/1234567"),
        ["greenhouse"] = new("https://boards.greenhouse.io/acme/jobs/1234567"),
        ["lever"] = new("https://jobs.lever.co/acme/5d2b1a3c-0000-4000-8000-000000000000"),
        ["linkedin"] = new("https://www.linkedin.com/jobs/view/1234567890"),
        ["fake"] = new("https://jobs.fake.test/acme/backend-developer"),
    };

    public static IEnumerable<Uri> ForeignTo(string connectorKey) =>
        Postings.Where(pair => pair.Key != connectorKey).Select(pair => pair.Value);
}
