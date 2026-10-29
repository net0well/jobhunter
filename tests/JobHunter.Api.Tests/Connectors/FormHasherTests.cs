using JobHunter.Connectors.Abstractions.Applications;

namespace JobHunter.Api.Tests.Connectors;

public sealed class FormHasherTests
{
    private static readonly FormField Email = new("email", "E-mail", FieldKind.Email, FieldSemantic.Email, true, [], null, null);

    private static readonly FormField English = new(
        "english",
        "Nível de inglês",
        FieldKind.SingleChoice,
        FieldSemantic.Custom,
        false,
        [new FieldOption("basic", "Básico"), new FieldOption("fluent", "Fluente")],
        null,
        null);

    [Fact]
    public void Compute_SameFields_ReturnsLowercaseSha256Hex()
    {
        var hash = FormHasher.Compute([Email, English]);

        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.Equal(hash, FormHasher.Compute([Email, English]));
    }

    [Fact]
    public void Compute_FieldsAndOptionsInDifferentOrder_ReturnsSameHash()
    {
        var reordered = English with { Options = [.. English.Options.Reverse()] };

        Assert.Equal(FormHasher.Compute([Email, English]), FormHasher.Compute([reordered, Email]));
    }

    [Fact]
    public void Compute_CosmeticChanges_ReturnsSameHash()
    {
        var relabeled = Email with { Label = "Seu e-mail", HelpText = "Usaremos para contato", Semantic = FieldSemantic.Custom, MaxLength = 120 };

        Assert.Equal(FormHasher.Compute([Email]), FormHasher.Compute([relabeled]));
    }

    [Fact]
    public void Compute_RequiredFlagChanges_ReturnsDifferentHash()
    {
        Assert.NotEqual(FormHasher.Compute([Email]), FormHasher.Compute([Email with { Required = false }]));
    }

    [Fact]
    public void Compute_KindChanges_ReturnsDifferentHash()
    {
        Assert.NotEqual(FormHasher.Compute([Email]), FormHasher.Compute([Email with { Kind = FieldKind.Text }]));
    }

    [Fact]
    public void Compute_OptionAdded_ReturnsDifferentHash()
    {
        var extended = English with { Options = [.. English.Options, new FieldOption("native", "Nativo")] };

        Assert.NotEqual(FormHasher.Compute([English]), FormHasher.Compute([extended]));
    }

    [Fact]
    public void Compute_FieldAdded_ReturnsDifferentHash()
    {
        Assert.NotEqual(FormHasher.Compute([Email]), FormHasher.Compute([Email, English]));
    }
}
