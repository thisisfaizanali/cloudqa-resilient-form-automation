using FormAutomation.Locating;
using OpenQA.Selenium;

namespace FormAutomation;

public record CheckResult(string Field, bool Passed, string Detail);

public static class FormChecks
{
    /// <summary>Fills each field, reads it back and compares. One failing check never stops the others.</summary>
    public static List<CheckResult> Run(IWebDriver driver, AppSettings settings, out string scopeNote)
    {
        PracticeForm form;
        try
        {
            driver.Navigate().GoToUrl(settings.BaseUrl);
            (form, scopeNote) = PracticeForm.Open(driver, TimeSpan.FromSeconds(settings.TimeoutSeconds));
        }
        catch (Exception ex)
        {
            scopeNote = "not resolved";
            return
            [
                new(PracticeForm.FirstNameLabel, false, ex.Message),
                new(PracticeForm.GenderLabel, false, ex.Message),
                new(PracticeForm.StateLabel, false, ex.Message),
            ];
        }

        var data = settings.TestData;
        return
        [
            Check(PracticeForm.FirstNameLabel, data.FirstName, () =>
            {
                form.SetFirstName(data.FirstName);
                return (form.FirstName, "");
            }),
            Check(PracticeForm.GenderLabel, data.Gender, () =>
            {
                form.SelectGender(data.Gender);
                var count = form.CheckedGenderCount;
                return (form.SelectedGender, count == 1 ? "" : $", but {count} radios are checked");
            }),
            Check(PracticeForm.StateLabel, data.State, () =>
            {
                form.SelectState(data.State);
                return (form.SelectedState, "");
            }),
        ];
    }

    private static CheckResult Check(string field, string expected, Func<(string? Got, string Problem)> act)
    {
        try
        {
            var (got, problem) = act();
            var passed = problem.Length == 0 && TextMatch.Normalize(got) == TextMatch.Normalize(expected);
            return new(field, passed, $"expected \"{expected}\", got \"{got}\"{problem}");
        }
        catch (Exception ex)
        {
            return new(field, false, ex.Message);
        }
    }
}
