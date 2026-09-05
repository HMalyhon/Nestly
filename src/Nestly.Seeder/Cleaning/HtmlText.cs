using System.Net;
using System.Text.RegularExpressions;

namespace Nestly.Seeder.Cleaning;

/// <summary>Turns the markup Airbnb hosts type into the plain text Elasticsearch should analyse.</summary>
internal static partial class HtmlText
{
    /// <summary>
    /// Deliberately narrower than <c>&lt;[^&gt;]+&gt;</c>. Descriptions contain prose like
    /// "&lt;25 lbs" -- with the loose pattern, that opening bracket pairs with the next real
    /// tag's closing bracket and swallows the sentence in between. Requiring a letter after the
    /// bracket, and forbidding another bracket inside, keeps the damage to genuine tags.
    /// </summary>
    [GeneratedRegex(@"</?[a-zA-Z][^<>]*>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TagPattern { get; }

    /// <summary>Collapses the runs of whitespace that stripping tags leaves behind, plus the non-breaking spaces the source is full of.</summary>
    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WhitespacePattern { get; }

    public static string Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        // Decode first, strip second. The other order looks equivalent and is not: entities are
        // invisible to the tag pattern, so "&lt;img onerror=...&gt;" would survive the strip and
        // the decode would then turn it back into live markup.
        var text = WebUtility.HtmlDecode(raw);

        // Tags become a space rather than nothing: "kitchen<br />Bedroom" is two sentences, and
        // deleting the tag outright would index "kitchenBedroom" as a single term.
        text = TagPattern.Replace(text, " ");

        return WhitespacePattern.Replace(text, " ").Trim();
    }
}
