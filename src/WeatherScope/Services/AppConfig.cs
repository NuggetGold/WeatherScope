namespace WeatherScope.Services;

public static class AppConfig
{
    public const string ApiKeyVariable = "YANDEX_WEATHER_API_KEY";
    public const string DefaultApiKey = "c7c8dc13-e91d-444a-90c7-c47c61f35238";
    public const string ForecastEndpoint = "https://api.weather.yandex.ru/v2/forecast";
    public const string InformersEndpoint = "https://api.weather.yandex.ru/v2/informers";
    public const string KeyHeader = "X-Yandex-Weather-Key";
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public static string ResolveApiKey(string? baseDirectory = null)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(ApiKeyVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment.Trim();
        }

        var directory = baseDirectory ?? AppContext.BaseDirectory;
        var fromFile = ReadEnvFile(Path.Combine(directory, ".env"));
        if (!string.IsNullOrWhiteSpace(fromFile))
        {
            return fromFile;
        }

        return DefaultApiKey;
    }

    public static string? ReadEnvFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();
            if (!string.Equals(name, ApiKeyVariable, StringComparison.Ordinal))
            {
                continue;
            }

            var value = line[(separator + 1)..].Trim().Trim('"', '\'');
            return value.Length == 0 ? null : value;
        }

        return null;
    }
}
