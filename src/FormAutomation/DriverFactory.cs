using OpenQA.Selenium.Chrome;

namespace FormAutomation;

public static class DriverFactory
{
    public static ChromeDriver Create(AppSettings settings)
    {
        var options = new ChromeOptions();
        if (settings.Headless)
        {
            options.AddArgument("--headless=new");
            options.AddArgument("--window-size=1366,900");
        }

        return new ChromeDriver(options);
    }
}
