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

    public static IWebElement FindRadioOption(ISearchContext scope, string groupLabel, string optionText)
    {
        var wanted = TextMatch.Normalize(optionText);
        var options = FindRadioGroup(scope, groupLabel).Select(r => (Radio: r, Text: OptionText(r))).ToList();
        var hits = options.Where(o => TextMatch.Normalize(o.Text) == wanted).ToList();
        if (hits.Count == 1) return hits[0].Radio;

        var available = string.Join(", ", options.Select(o => $"\"{o.Text}\""));
        throw new FieldResolutionException(
            $"{(hits.Count == 0 ? "No" : "Ambiguous")} radio option \"{optionText}\" in group \"{groupLabel}\": " +
            $"{hits.Count} matches. Available options: {available}");
    }

    private static Candidate Resolve(ISearchContext scope, string label, Kind kind)
    {
        var candidates = Candidates(scope, kind);
        if (candidates.Count > 0)
        {
            AddProximity(candidates, kind, Anchors(scope, label));
            AddExplicitAssociation(candidates, kind, label);
            AddAttributeHints(candidates, label);
        }
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

    /// <summary>100 points when a real label, aria attribute or fieldset legend names the control.</summary>
    private static void AddExplicitAssociation(List<Candidate> candidates, Kind kind, string label)
    {
        var wanted = TextMatch.Normalize(label);
        foreach (var c in candidates)
        {
            var reason = kind.Grouped ? GroupAssociation(c.Primary, wanted) : ControlAssociation(c.Primary, wanted);
            if (reason is not null) c.Add(100, reason);
        }
    }

    private static string? ControlAssociation(IWebElement element, string wanted)
    {
        var id = element.GetDomAttribute("id");
        if (!string.IsNullOrEmpty(id) &&
            element.FindElements(By.XPath($"//label[@for={Literal(id)}]")).Any(l => OwnText(l) == wanted))
            return "label-for";
        if (element.FindElements(By.XPath("ancestor::label")).Any(l => OwnText(l) == wanted))
            return "wrapping-label";
        return AriaAssociation(element, wanted);
    }

    private static string? GroupAssociation(IWebElement firstRadio, string wanted)
    {
        foreach (var fieldset in firstRadio.FindElements(By.XPath("ancestor::fieldset[1]")))
        {
            if (fieldset.FindElements(By.XPath("./legend")).Any(l => OwnText(l) == wanted)) return "legend";
            if (AriaAssociation(fieldset, wanted) is { } viaFieldset) return viaFieldset;
        }
        foreach (var group in firstRadio.FindElements(By.XPath("ancestor::*[@role='radiogroup'][1]")))
            if (AriaAssociation(group, wanted) is { } viaRole) return viaRole;
        return null;
    }

    private static string? AriaAssociation(IWebElement element, string wanted)
    {
        if (TextMatch.Normalize(element.GetDomAttribute("aria-label")) == wanted) return "aria-label";

        var ids = (element.GetDomAttribute("aria-labelledby") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var id in ids)
            if (element.FindElements(By.XPath($"//*[@id={Literal(id)}]")).Any(e => OwnText(e) == wanted))
                return "aria-labelledby";
        return null;
    }

    /// <summary>
    /// Visible text of a radio: wrapping label, then label[for], then the text after it up to the next
    /// control (for-attributes on spans are ignored on purpose), then its value.
    /// </summary>
    private static string OptionText(IWebElement radio)
    {
        var wrapping = radio.FindElements(By.XPath("ancestor::label")).Select(OwnText).FirstOrDefault(t => t.Length > 0);
        if (wrapping is not null) return wrapping;

        var id = radio.GetDomAttribute("id");
        if (!string.IsNullOrEmpty(id))
        {
            var linked = radio.FindElements(By.XPath($"//label[@for={Literal(id)}]")).Select(OwnText).FirstOrDefault(t => t.Length > 0);
            if (linked is not null) return linked;
        }

        var js = (IJavaScriptExecutor)((IWrapsDriver)radio).WrappedDriver;
        var following = js.ExecuteScript(
            "var n = arguments[0].nextSibling, t = '', sel = 'input,select,textarea,button';" +
            "while (n) {" +
            "  if (n.nodeType === 1 && (n.matches(sel) || n.querySelector(sel))) break;" +
            "  t += n.textContent; n = n.nextSibling;" +
            "}" +
            "return t;", radio) as string;
        var text = TextMatch.Normalize(following);
        return text.Length > 0 ? text : TextMatch.Normalize(radio.GetDomAttribute("value"));
    }

    /// <summary>Normalized text of an element without the form controls it wraps.</summary>
    private static string OwnText(IWebElement element)
    {
        var js = (IJavaScriptExecutor)((IWrapsDriver)element).WrappedDriver;
        var text = js.ExecuteScript(
            "var c = arguments[0].cloneNode(true);" +
            "c.querySelectorAll('input,select,textarea,button').forEach(function (e) { e.remove(); });" +
            "return c.textContent;", element) as string;
        return TextMatch.Normalize(text);
    }

    /// <summary>40 points for a matching placeholder or title, 20 for an id/name that reads as the label.</summary>
    private static void AddAttributeHints(List<Candidate> candidates, string label)
    {
        var wanted = TextMatch.Normalize(label);
        foreach (var c in candidates)
        {
            var e = c.Primary;
            if (TextMatch.Normalize(e.GetDomAttribute("placeholder")) == wanted) c.Add(40, "placeholder");
            else if (TextMatch.Normalize(e.GetDomAttribute("title")) == wanted) c.Add(40, "title");

            if (TextMatch.MatchesIdentifier(e.GetDomAttribute("name"), label) ||
                TextMatch.MatchesIdentifier(e.GetDomAttribute("id"), label))
                c.Add(20, "name/id");
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
