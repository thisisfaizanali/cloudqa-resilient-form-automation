using System.Text.RegularExpressions;

namespace FormAutomation.Locating;

public static class TextMatch
{
    /// <summary>Lowercase, nbsp to space, collapsed whitespace, no leading/trailing punctuation.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var s = Regex.Replace(text.Replace('\u00A0', ' ').ToLowerInvariant(), @"\s+", " ");
        int start = 0, end = s.Length;
        while (start < end && IsEdgeNoise(s[start])) start++;
        while (end > start && IsEdgeNoise(s[end - 1])) end--;
        return s[start..end];
    }

    /// <summary>True when an id/name such as first_name or firstName reads as the label.</summary>
    public static bool MatchesIdentifier(string? attr, string label)
    {
        if (string.IsNullOrWhiteSpace(attr)) return false;
        var words = Regex.Replace(attr, @"(?<=[a-z0-9])(?=[A-Z])", " ").Replace('_', ' ').Replace('-', ' ');
        var normalizedLabel = Normalize(label);
        return normalizedLabel.Length > 0 && Normalize(words) == normalizedLabel;
    }

    private static bool IsEdgeNoise(char c) => char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c);
}
