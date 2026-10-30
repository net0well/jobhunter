using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace JobHunter.Connectors.InHire.Parsing;

// Turns job description HTML into readable plain text, keeping paragraph and list breaks that scoring and review rely on.
internal static class HtmlText
{
    private static readonly HashSet<string> BlockElements =
        ["P", "DIV", "LI", "UL", "OL", "H1", "H2", "H3", "H4", "H5", "H6", "BR", "TR", "SECTION", "ARTICLE", "BLOCKQUOTE"];

    private const string ListMarker = "- ";

    private static readonly HtmlParser Parser = new();

    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var document = Parser.ParseDocument(html);
        var builder = new StringBuilder();
        Append(document.Body!, builder);

        return string.Join('\n', builder.ToString()
            .Split('\n')
            .Select(line => string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(line => line.Length > 0));
    }

    private static void Append(INode node, StringBuilder builder)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child)
            {
                case IText text:
                    builder.Append(text.Data);
                    break;

                case IElement element:
                    var block = BlockElements.Contains(element.TagName);
                    if (block)
                        BreakLine(builder);

                    if (element.TagName == "LI")
                        builder.Append(ListMarker);

                    Append(element, builder);

                    if (block)
                        BreakLine(builder);
                    break;
            }
        }
    }

    // Editors often wrap list items in paragraphs (<li><p>…</p></li>); the item text must stay on the marker's line.
    private static void BreakLine(StringBuilder builder)
    {
        if (builder.Length > 0 && !builder.ToString().EndsWith(ListMarker, StringComparison.Ordinal))
            builder.Append('\n');
    }
}
