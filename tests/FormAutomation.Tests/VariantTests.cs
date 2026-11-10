using OpenQA.Selenium;

namespace FormAutomation.Tests;

/// <summary>Runs the same FormChecks used by the console against local copies of the form with different markup.</summary>
public class VariantTests
{
    private IWebDriver _driver = null!;

    [OneTimeSetUp]
    public void StartDriver() => _driver = DriverFactory.Create(AppSettings.Load([]) with { Headless = true });

    [OneTimeTearDown]
    public void StopDriver() => _driver.Dispose();

    private List<CheckResult> Run(string variant, string firstName, string gender, string state)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "variants", variant + ".html");
        var settings = AppSettings.Load([]) with
        {
            BaseUrl = new Uri(path).AbsoluteUri,
            Headless = true,
            TimeoutSeconds = 5,
            TestData = new TestDataSettings { FirstName = firstName, Gender = gender, State = state },
        };
        return FormChecks.Run(_driver, settings, out _);
    }

    private static string Details(List<CheckResult> results)
        => string.Join(Environment.NewLine, results.Select(r => $"{(r.Passed ? "PASS" : "FAIL")} {r.Field}: {r.Detail}"));

    [Test, Combinatorial]
    public void AllChecksPass(
        [Values("baseline", "reordered", "renamed-attributes", "restructured", "lookalikes", "combined", "swapped-ids")] string variant,
        [Values("John|Male|India", "Faizan|Transgender|Afganistan")] string data)
    {
        var d = data.Split('|');
        var results = Run(variant, d[0], d[1], d[2]);
        Assert.That(results, Has.All.Property(nameof(CheckResult.Passed)).True, Details(results));
    }

    [Test]
    public void SwappedIds_FillsTheBoxNextToTheFirstNameLabel()
    {
        var results = Run("swapped-ids", "John", "Male", "India");
        Assert.That(results, Has.All.Property(nameof(CheckResult.Passed)).True, Details(results));
        // The input with id "fname" is the Last Name box in this variant; "lname" is the First Name box.
        Assert.That(_driver.FindElement(By.Id("fname")).GetDomProperty("value"), Is.Empty);
        Assert.That(_driver.FindElement(By.Id("lname")).GetDomProperty("value"), Is.EqualTo("John"));
    }

    [Test]
    public void MissingState_FailsEveryCheckWithAClearMessage()
    {
        var results = Run("missing-state", "John", "Male", "India");
        Assert.That(results, Has.Count.EqualTo(3), Details(results));
        Assert.That(results, Has.All.Property(nameof(CheckResult.Passed)).False, Details(results));
        Assert.That(results.Select(r => r.Detail),
            Has.All.StartsWith("Form not resolved").And.Contain("State"), Details(results));
    }
}
