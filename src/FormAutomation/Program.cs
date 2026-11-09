using FormAutomation;

try
{
    var settings = AppSettings.Load(args);
    var driver = DriverFactory.Create(settings);
    try
    {
        var results = FormChecks.Run(driver, settings, out var scopeNote);
        Console.WriteLine($"Page:  {settings.BaseUrl}");
        Console.WriteLine($"Scope: {scopeNote}");
        foreach (var r in results)
            Console.WriteLine($"{(r.Passed ? "PASS" : "FAIL")}  {r.Field,-10}  {r.Detail}");

        var passed = results.Count(r => r.Passed);
        Console.WriteLine($"Result: {passed}/{results.Count} passed");
        return passed == results.Count ? 0 : 1;
    }
    finally
    {
        driver.Quit();
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
