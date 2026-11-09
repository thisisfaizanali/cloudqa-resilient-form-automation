using FormAutomation.Locating;

namespace FormAutomation.Tests;

public class TextMatchTests
{
    [TestCase("  First Name *:", "first name")]
    [TestCase("FIRST NAME:", "first name")]
    [TestCase("First\u00A0 Name", "first name")]
    [TestCase(" State ", "state")]
    [TestCase("(Gender)", "gender")]
    [TestCase("# Email #", "email")]
    [TestCase("Father's First Name", "father's first name")]
    [TestCase(null, "")]
    public void Normalize_CleansText(string? input, string expected)
        => Assert.That(TextMatch.Normalize(input), Is.EqualTo(expected));

    [TestCase("first_name")]
    [TestCase("firstName")]
    [TestCase("First Name")]
    [TestCase("first-name")]
    [TestCase("FirstName")]
    public void MatchesIdentifier_Matches(string attr)
        => Assert.That(TextMatch.MatchesIdentifier(attr, "First Name"), Is.True);

    [TestCase("fname")]
    [TestCase("last_name")]
    [TestCase("")]
    [TestCase(null)]
    public void MatchesIdentifier_DoesNotMatch(string? attr)
        => Assert.That(TextMatch.MatchesIdentifier(attr, "First Name"), Is.False);
}
