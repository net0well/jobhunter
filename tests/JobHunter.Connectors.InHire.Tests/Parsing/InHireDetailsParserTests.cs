using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.InHire.Parsing;
using JobHunter.Connectors.InHire.Tests.Infrastructure;

namespace JobHunter.Connectors.InHire.Tests.Parsing;

public sealed class InHireDetailsParserTests
{
    [Fact]
    public Task Parse_JobPage_ReturnsPlainTextDescriptionAndJobUrl()
    {
        var result = InHireDetailsParser.Parse("eyxo", File.ReadAllText(InHireTestHost.Fixture("job-eyxo-5f8b856e.json")));

        Assert.True(result.IsSuccess, result.Error?.Message);
        var details = result.Value!;
        Assert.DoesNotContain("<", details.DescriptionText, StringComparison.Ordinal);
        Assert.Contains("- Planejar campanhas com criadores de conteúdo;", details.DescriptionText, StringComparison.Ordinal);
        Assert.Contains("indicadores & resultados", details.DescriptionText, StringComparison.Ordinal);
        Assert.Equal(InHireTestHost.JobUrl(InHireTestHost.OpenJobId), details.ApplyUrl);
        Assert.Equal("PJ", details.Extra["contractType"]);
        Assert.Equal("presencial 1x por semana", details.Extra["locationComplement"]);

        return Verify(details);
    }

    [Fact]
    public void Parse_JobNoLongerPublished_ReturnsPostingClosed()
    {
        var result = InHireDetailsParser.Parse("eyxo", """{"jobId":"a","displayName":"A","status":"closed","description":"<p>x</p>"}""");

        Assert.Equal(ConnectorErrorKind.PostingClosed, result.Error?.Kind);
    }

    [Theory]
    [InlineData("""{"displayName":"Sem id"}""")]
    [InlineData("[]")]
    [InlineData("not json")]
    public void Parse_UnexpectedShape_ReturnsLayoutChanged(string json)
    {
        Assert.Equal(ConnectorErrorKind.LayoutChanged, InHireDetailsParser.Parse("eyxo", json).Error?.Kind);
    }

    [Theory]
    [InlineData("<p>Um</p><p>Dois</p>", "Um\nDois")]
    [InlineData("<ul><li>A</li><li>B</li></ul>", "- A\n- B")]
    [InlineData("<p>Linha<br>quebrada</p>", "Linha\nquebrada")]
    [InlineData("<p>  espaços   demais </p>", "espaços demais")]
    [InlineData(null, "")]
    public void ToPlainText_Html_KeepsLineBreaksAndDropsMarkup(string? html, string expected)
    {
        Assert.Equal(expected, HtmlText.ToPlainText(html));
    }
}
