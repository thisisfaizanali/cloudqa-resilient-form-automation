using FormAutomation.Locating;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace FormAutomation;

/// <summary>First Name, Gender and State of the practice form. Every element is resolved fresh on each use.</summary>
public class PracticeForm(ISearchContext scope)
{
    public const string FirstNameLabel = "First Name";
    public const string GenderLabel = "Gender";
    public const string StateLabel = "State";

    private ISearchContext Scope { get; } = scope;

    /// <summary>
    /// Waits for the form that holds all three fields. Iframes and shadow roots are never entered, on purpose:
    /// their copies of the fields are not part of the main form.
    /// </summary>
    public static (PracticeForm Form, string ScopeNote) Open(IWebDriver driver, TimeSpan timeout)
    {
        FieldResolutionException? last = null;
        try
        {
            var opened = new WebDriverWait(driver, timeout).Until(d =>
            {
                try { return TryOpen(d, out last); }
                catch (StaleElementReferenceException) { return null; }
            })!;
            return (opened.Form, opened.Note);
        }
        catch (WebDriverTimeoutException)
        {
            throw last ?? new FieldResolutionException($"Timed out after {timeout.TotalSeconds:0}s waiting for the page to settle");
        }
    }

    private sealed record Opened(PracticeForm Form, string Note);

    private static Opened? TryOpen(IWebDriver driver, out FieldResolutionException? error)
    {
        error = null;
        var forms = driver.FindElements(By.TagName("form")).Where(f => f.Displayed).ToList();
        var holders = new List<(IWebElement Form, int Number)>();
        for (var i = 0; i < forms.Count; i++)
        {
            try
            {
                ResolveAll(forms[i]);
                holders.Add((forms[i], i + 1));
            }
            catch (FieldResolutionException) { }
        }

        if (holders.Count == 1)
            return new(new PracticeForm(holders[0].Form), $"form {holders[0].Number} of {forms.Count} on the page");
        if (holders.Count > 1)
            return new(new PracticeForm(holders[0].Form), $"{holders.Count} forms contain all three fields, using the first");

        try
        {
            ResolveAll(driver);
            return new(new PracticeForm(driver), "no single form, searched the whole document");
        }
        catch (FieldResolutionException ex)
        {
            error = ex;
            return null;
        }
    }

    private static void ResolveAll(ISearchContext scope)
    {
        FieldLocator.FindTextBox(scope, FirstNameLabel);
        FieldLocator.FindRadioGroup(scope, GenderLabel);
        FieldLocator.FindSelect(scope, StateLabel);
    }

    public void SetFirstName(string value)
    {
        var box = FieldLocator.FindTextBox(Scope, FirstNameLabel);
        box.Clear();
        box.SendKeys(value);
    }

    public string FirstName => FieldLocator.FindTextBox(Scope, FirstNameLabel).GetDomProperty("value") ?? "";

    public void SelectGender(string option)
        => FieldLocator.FindRadioOption(Scope, GenderLabel, option).Click();

    public string? SelectedGender
    {
        get
        {
            var checkedRadio = FieldLocator.FindRadioGroup(Scope, GenderLabel).FirstOrDefault(r => r.Selected);
            return checkedRadio is null ? null : FieldLocator.RadioOptionText(checkedRadio);
        }
    }

    public int CheckedGenderCount => FieldLocator.FindRadioGroup(Scope, GenderLabel).Count(r => r.Selected);

    public void SelectState(string option)
    {
        var select = new SelectElement(FieldLocator.FindSelect(Scope, StateLabel));
        var wanted = TextMatch.Normalize(option);
        var texts = select.Options.Select(o => o.Text).ToList();
        var index = texts.FindIndex(t => TextMatch.Normalize(t) == wanted);
        if (index < 0)
        {
            var shown = string.Join(", ", texts.Take(10).Select(t => $"\"{t.Trim()}\""));
            throw new FieldResolutionException(
                $"No option \"{option}\" in the {StateLabel} select: {texts.Count} options, first {Math.Min(10, texts.Count)}: {shown}");
        }
        select.SelectByIndex(index);
    }

    public string? SelectedState
        => new SelectElement(FieldLocator.FindSelect(Scope, StateLabel)).SelectedOption.Text.Trim();
}
