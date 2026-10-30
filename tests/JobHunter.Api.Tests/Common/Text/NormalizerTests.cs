using JobHunter.Api.Common.Text;

namespace JobHunter.Api.Tests.Common.Text;

public sealed class NormalizerTests
{
    [Theory]
    [InlineData("  Desenvolvedor   .NET  Sênior ", "desenvolvedor .net senior")]
    [InlineData("ESTÁGIO em Ciência de Dados", "estagio em ciencia de dados")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Text_AccentsCaseAndSpacing_AreNormalized(string? input, string expected)
    {
        Assert.Equal(expected, Normalizer.Text(input));
    }

    [Theory]
    [InlineData("Acme Tecnologia Ltda.", "acme tecnologia")]
    [InlineData("Acme Tecnologia LTDA", "acme tecnologia")]
    [InlineData("Banco Ação S/A", "banco acao")]
    [InlineData("Globex Inc.", "globex")]
    [InlineData("Initech S.A.", "initech")]
    [InlineData("ME", "me")]
    public void CompanyName_LegalSuffixes_AreRemoved(string input, string expected)
    {
        Assert.Equal(expected, Normalizer.CompanyName(input));
    }

    [Theory]
    [InlineData("São Paulo, SP", "sao paulo")]
    [InlineData("Belo Horizonte - MG", "belo horizonte")]
    [InlineData("Curitiba / PR (Híbrido)", "curitiba")]
    [InlineData("Remoto", null)]
    [InlineData("100% Home Office", null)]
    [InlineData(null, null)]
    public void City_Location_ExtractsCityOrNullWhenRemote(string? location, string? expected)
    {
        Assert.Equal(expected, Normalizer.City(location));
    }

    [Theory]
    [InlineData("Desenvolvedor .NET Pleno", ".NET", true)]
    [InlineData("Experiência com C# e SQL.", "c#", true)]
    [InlineData("Vaga JavaScript", "java", false)]
    [InlineData("Node.js developer", "node.js", true)]
    [InlineData("Trabalho em home office", "home office", true)]
    [InlineData("Estágio em TI", "estágio", true)]
    [InlineData("Estagiário de TI", "estágio", false)]
    public void ContainsTerm_WholeTokens_MatchesOnlyCompleteTerms(string text, string term, bool expected)
    {
        Assert.Equal(expected, Normalizer.ContainsTerm(text, term));
    }

    [Fact]
    public void Fingerprint_SamePostingWrittenDifferently_IsEqual()
    {
        var first = Normalizer.Fingerprint("acme", "Desenvolvedor .NET Sênior", "São Paulo, SP");
        var second = Normalizer.Fingerprint("acme", "desenvolvedor .net  senior", "Sao Paulo - SP");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Fingerprint_DifferentCity_IsDifferent()
    {
        Assert.NotEqual(
            Normalizer.Fingerprint("acme", "Desenvolvedor .NET", "São Paulo, SP"),
            Normalizer.Fingerprint("acme", "Desenvolvedor .NET", "Recife, PE"));
    }
}
