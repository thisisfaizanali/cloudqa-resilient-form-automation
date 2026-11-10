using FormAutomation.Locating;
using OpenQA.Selenium;

namespace FormAutomation.Tests;

public class PracticeFormTests
{
    private AppSettings _settings = null!;
    private IWebDriver _driver = null!;
    private PracticeForm _form = null!;

    [OneTimeSetUp]
    public void StartDriver()
    {
        _settings = AppSettings.Load([]) with { Headless = true };
        _driver = DriverFactory.Create(_settings);
    }

    [OneTimeTearDown]
    public void StopDriver() => _driver.Dispose();

    [SetUp]
    public void OpenPage()
    {
        _driver.Navigate().GoToUrl(_settings.BaseUrl);
        _form = PracticeForm.Open(_driver, TimeSpan.FromSeconds(_settings.TimeoutSeconds)).Form;
    }

    [Test]
    public void FirstName_RoundTrip()
    {
        _form.SetFirstName("John");
        _form.SetFirstName("Faizan");
        Assert.That(_form.FirstName, Is.EqualTo("Faizan"));
    }

    [TestCase("Male")]
    [TestCase("Female")]
    [TestCase("Transgender")]
    public void Gender_SelectsOnlyTheRequestedOption(string option)
    {
        _form.SelectGender(option);
        Assert.That(_form.SelectedGender, Is.EqualTo(option));
        Assert.That(_form.CheckedGenderCount, Is.EqualTo(1));
    }

    [Test]
    public void Gender_SwitchingLeavesOnlyTheLastChoice()
    {
        _form.SelectGender("male");
        _form.SelectGender("female");
        Assert.That(_form.SelectedGender, Is.EqualTo("Female"));
        Assert.That(_form.CheckedGenderCount, Is.EqualTo(1));
    }

    [TestCase("Afganistan")]
    [TestCase("India")]
    public void State_SelectsPaddedAndNormalOptions(string option)
    {
        _form.SelectState(option);
        Assert.That(_form.SelectedState, Is.EqualTo(option));
    }

    [Test]
    public void State_UnknownOptionListsAvailableOnes()
    {
        var ex = Assert.Throws<FieldResolutionException>(() => _form.SelectState("Atlantis"))!;
        Assert.That(ex.Message, Does.Contain("Atlantis").And.Contain("Afganistan"));
    }

    [Test]
    public void FormChecks_AllPassWithDefaultSettings()
    {
        var results = FormChecks.Run(_driver, _settings, out _);
        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results.Where(r => !r.Passed), Is.Empty);
    }
}
