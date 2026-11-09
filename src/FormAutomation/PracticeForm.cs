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
