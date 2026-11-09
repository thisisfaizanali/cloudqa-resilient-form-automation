namespace FormAutomation.Tests;

public class AppSettingsTests
{
    [TestCase("ftp://x")]
    [TestCase("not-a-url")]
    public void Load_RejectsUnsupportedUrls(string url)
        => Assert.Throws<InvalidOperationException>(() => AppSettings.Load(["--url", url]));

    [Test]
    public void Load_AcceptsFileUrls()
        => Assert.That(AppSettings.Load(["--url", "file:///C:/x.html"]).BaseUrl, Is.EqualTo("file:///C:/x.html"));
}
