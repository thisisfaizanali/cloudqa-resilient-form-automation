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
        [Values("baseline", "reordered", "renamed-attributes", "restructured", "lookalikes", "combined")] string variant,
        [Values("John|Male|India", "Faizan|Transgender|Afganistan")] string data)
    {
        var d = data.Split('|');
        var results = Run(variant, d[0], d[1], d[2]);
        Assert.That(results, Has.All.Property(nameof(CheckResult.Passed)).True, Details(results));
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
