using Microsoft.Extensions.Configuration;

namespace FormAutomation;

public record AppSettings
{
    public string BaseUrl { get; init; } = "";
    public bool Headless { get; init; }
    public int TimeoutSeconds { get; init; }

    private static readonly Dictionary<string, string> SwitchMappings = new()
    {
        ["--url"] = nameof(BaseUrl),
        ["--headless"] = nameof(Headless),
        ["--timeout"] = nameof(TimeoutSeconds),
    };

    // Precedence, lowest to highest: appsettings.json, CLOUDQA_ environment variables, command line.
    public static AppSettings Load(string[] args)
    {
        var settings = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables("CLOUDQA_")
            .AddCommandLine(args, SwitchMappings)
            .Build()
            .Get<AppSettings>() ?? new AppSettings();

        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https" or "file"))
        {
            throw new InvalidOperationException(
                $"BaseUrl must be an absolute URL (http, https or file), got: '{settings.BaseUrl}'");
        }

        return settings;
    }
}
