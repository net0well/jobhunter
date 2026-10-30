using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace JobHunter.Api.Common.Text;

internal static class Normalizer
{
    private static readonly HashSet<string> CompanySuffixes =
        ["ltda", "ltd", "sa", "s/a", "s.a", "me", "epp", "eireli", "inc", "llc", "corp", "corporation", "gmbh"];

    private static readonly string[] RemoteMarkers = ["remoto", "remote", "home office", "anywhere"];

    private static readonly char[] CitySeparators = [',', '-', '/', '(', '|'];

    // Lowercase, accent-free text with single spaces: the base for every comparison in matching and deduplication.
    public static string Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;

        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    public static string CompanyName(string? name)
    {
        var tokens = Tokens(name).ToList();

        while (tokens.Count > 1 && CompanySuffixes.Contains(tokens[^1]))
        {
            tokens.RemoveAt(tokens.Count - 1);
        }

        return string.Join(' ', tokens);
    }

    public static string? City(string? location)
    {
        var text = Text(location);
        if (text.Length == 0 || MentionsRemote(text))
            return null;

        var separator = text.IndexOfAny(CitySeparators);
        var city = (separator >= 0 ? text[..separator] : text).Trim();
        return city.Length == 0 ? null : city;
    }

    public static bool MentionsRemote(string? text)
    {
        var normalized = Text(text);
        return RemoteMarkers.Any(marker => ContainsTerm(normalized, marker));
    }

    // Token-based so that "java" does not match "javascript" while ".net", "c#" and "node.js" still work as terms.
    public static bool ContainsTerm(string? text, string term)
    {
        var haystack = Tokens(text).ToArray();
        var needle = Tokens(term).ToArray();

        if (needle.Length == 0 || needle.Length > haystack.Length)
            return false;

        for (var start = 0; start <= haystack.Length - needle.Length; start++)
        {
            if (haystack.AsSpan(start, needle.Length).SequenceEqual(needle))
                return true;
        }

        return false;
    }

    public static string Fingerprint(string normalizedCompany, string title, string? location)
    {
        var key = $"{normalizedCompany}|{Text(title)}|{City(location)}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }

    private static IEnumerable<string> Tokens(string? value)
    {
        var text = Text(value);
        var token = new StringBuilder();

        foreach (var character in text.Append(' '))
        {
            if (char.IsLetterOrDigit(character) || character is '#' or '+' or '.' or '/')
            {
                token.Append(character);
                continue;
            }

            var completed = token.ToString().TrimEnd('.', '/');
            token.Clear();

            if (completed.Length > 0)
                yield return completed;
        }
    }
}
