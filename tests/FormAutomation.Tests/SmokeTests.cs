using FormAutomation;
using OpenQA.Selenium;

namespace FormAutomation.Tests;

public class SmokeTests
{
    private IWebDriver _driver = null!;

    [SetUp]
    public void SetUp()
    {
        var settings = AppSettings.Load([]) with { Headless = true };
        _driver = DriverFactory.Create(settings);
        _driver.Navigate().GoToUrl(settings.BaseUrl);
    }

    [TearDown]
    public void TearDown() => _driver.Dispose();

    [Test]
    public void ConfiguredPage_HasAForm()
    {
        Assert.That(_driver.FindElements(By.TagName("form")), Is.Not.Empty);
    }
}
