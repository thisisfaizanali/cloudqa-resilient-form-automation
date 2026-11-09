using OpenQA.Selenium;

namespace FormAutomation.Locating;

/// <summary>
/// Finds form controls from their visible label text. Ids, names, classes and page structure
/// are never assumed; the label is the only input.
/// </summary>
public static class FieldLocator
{
    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Lower = "abcdefghijklmnopqrstuvwxyz";

    private sealed record Kind(string Name, string XPath, bool Grouped);

    private static readonly Kind TextBoxKind = new("text box",
        $".//input[not(@type) or {Lowered("@type")}='' or {Lowered("@type")}='text' or {Lowered("@type")}='email' " +
        $"or {Lowered("@type")}='tel' or {Lowered("@type")}='search' or {Lowered("@type")}='url']", false);
    private static readonly Kind SelectKind = new("select", ".//select", false);
    private static readonly Kind RadioKind = new("radio group", $".//input[{Lowered("@type")}='radio']", true);

    private sealed class Candidate(IReadOnlyList<IWebElement> members)
    {
        public IReadOnlyList<IWebElement> Members { get; } = members;
        public IWebElement Primary => Members[0];
        public int Score { get; private set; }
        public List<string> Reasons { get; } = [];

        public void Add(int points, string reason)
        {
            Score += points;
            Reasons.Add(reason);
        }

        public CandidateInfo Info() => new(Primary.TagName, Primary.GetDomAttribute("type"),
            Primary.GetDomAttribute("id"), Primary.GetDomAttribute("name"), Score, string.Join(", ", Reasons));
    }

    public static IWebElement FindTextBox(ISearchContext scope, string label)
        => Resolve(scope, label, TextBoxKind).Primary;

    public static IWebElement FindSelect(ISearchContext scope, string label)
        => Resolve(scope, label, SelectKind).Primary;

    public static IReadOnlyList<IWebElement> FindRadioGroup(ISearchContext scope, string label)
        => Resolve(scope, label, RadioKind).Members;

    private static Candidate Resolve(ISearchContext scope, string label, Kind kind)
    {
        var candidates = Candidates(scope, kind);
        if (candidates.Count > 0)
            AddProximity(candidates, kind, Anchors(scope, label));
        return Choose(kind, label, candidates);
    }

    private static List<Candidate> Candidates(ISearchContext scope, Kind kind)
    {
        var elements = Displayed(scope, kind);
        if (!kind.Grouped) return elements.Select(e => new Candidate([e])).ToList();

        // Radios share a group by name; nameless radios share their parent element.
        return elements
            .GroupBy(r => GroupKey(r))
            .Select(g => new Candidate(g.ToList()))
            .ToList();
    }

    private static (string?, IWebElement?) GroupKey(IWebElement radio)
    {
        var name = radio.GetDomAttribute("name");
        return string.IsNullOrEmpty(name) ? (null, radio.FindElement(By.XPath(".."))) : (name, null);
    }

    private static List<IWebElement> Displayed(ISearchContext context, Kind kind)
        => context.FindElements(By.XPath(kind.XPath)).Where(e => e.Displayed).ToList();

    /// <summary>Visible elements whose own text equals the label after normalization.</summary>
    private static List<IWebElement> Anchors(ISearchContext scope, string label)
    {
        var wanted = TextMatch.Normalize(label);
        var words = wanted.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return [];

        var matchesWords = string.Join(" and ", words.Select(w => $"contains({Lowered(".")},{Literal(w)})"));
        var xpath = $".//*[not(self::script or self::style or self::option)][text()[{matchesWords}]]";
        return scope.FindElements(By.XPath(xpath))
            .Where(e => TextMatch.Normalize(e.Text) == wanted)
            .ToList();
    }

    /// <summary>
    /// From each anchor walk up to the first ancestor holding a candidate. If it holds exactly one,
    /// that candidate earns the proximity points; if several, the anchor says nothing.
    /// </summary>
    private static void AddProximity(List<Candidate> candidates, Kind kind, List<IWebElement> anchors)
    {
        foreach (var anchor in anchors)
        {
            for (IWebElement? node = anchor; node is not null; node = Parent(node))
            {
                var present = Displayed(node, kind).ToHashSet();
                var inside = candidates.Where(c => c.Members.All(present.Contains)).ToList();
                if (inside.Count == 0) continue;
                if (inside.Count == 1) inside[0].Add(80, "proximity");
                break;
            }
        }
    }

    private static IWebElement? Parent(IWebElement node)
        => node.FindElements(By.XPath("..")).FirstOrDefault();

    private static Candidate Choose(Kind kind, string label, List<Candidate> candidates)
    {
        var ranked = candidates.OrderByDescending(c => c.Score).ToList();
        if (ranked.Count == 0 || ranked[0].Score == 0)
            throw FieldResolutionException.NotFound(kind.Name, label, ranked.Select(c => c.Info()).ToList());

        var tied = ranked.Where(c => c.Score == ranked[0].Score).ToList();
        if (tied.Count > 1)
            throw FieldResolutionException.Ambiguous(kind.Name, label, ranked[0].Score, tied.Select(c => c.Info()).ToList());

        return ranked[0];
    }

    private static string Lowered(string expression) => $"translate({expression},'{Upper}','{Lower}')";

    /// <summary>XPath string literal that is safe for any quote characters.</summary>
    private static string Literal(string text)
        => !text.Contains('\'') ? $"'{text}'"
         : !text.Contains('"') ? $"\"{text}\""
         : "concat('" + text.Replace("'", "', \"'\", '") + "')";
}
