using JobHunter.Api.Common.Text;

namespace JobHunter.Api.Domain;

internal sealed class Company : IHasCreatedAt
{
    private Company(Guid id, string name, string normalizedName)
    {
        Id = id;
        Name = name;
        NormalizedName = normalizedName;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public string? Website { get; private set; }

    public string? Notes { get; private set; }

    public bool IsBlocked { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Company Create(string name, DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), name.Trim(), Normalizer.CompanyName(name));

    public void Block() => IsBlocked = true;

    public void Unblock() => IsBlocked = false;
}
