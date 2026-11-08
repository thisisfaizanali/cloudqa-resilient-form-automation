using FormAutomation;

try
{
    var settings = AppSettings.Load(args);
    var driver = DriverFactory.Create(settings);
    try
    {
        driver.Navigate().GoToUrl(settings.BaseUrl);
        Console.WriteLine(driver.Title);
    }
    finally
    {
        driver.Quit();
    }
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
