using WeatherScope.Models;
using WeatherScope.Services;
using Xunit;

namespace WeatherScope.Tests;

[Collection("EnvironmentSensitive")]
public class ConfigAndStorageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "weatherscope-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string? _previousKey = Environment.GetEnvironmentVariable(AppConfig.ApiKeyVariable);

    public ConfigAndStorageTests()
    {
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable(AppConfig.ApiKeyVariable, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(AppConfig.ApiKeyVariable, _previousKey);
        Directory.Delete(_dir, true);
    }

    [Fact]
    public void ApiKey_FallsBackToDefault()
    {
        Assert.Equal(AppConfig.DefaultApiKey, AppConfig.ResolveApiKey(_dir));
    }

    [Fact]
    public void ApiKey_ReadsEnvFile()
    {
        File.WriteAllText(Path.Combine(_dir, ".env"), "# комментарий\nOTHER=1\nYANDEX_WEATHER_API_KEY=\"file-key\"\n");
        Assert.Equal("file-key", AppConfig.ResolveApiKey(_dir));
    }

    [Fact]
    public void ApiKey_EmptyEnvFileValueIsIgnored()
    {
        File.WriteAllText(Path.Combine(_dir, ".env"), "YANDEX_WEATHER_API_KEY=\n");
        Assert.Equal(AppConfig.DefaultApiKey, AppConfig.ResolveApiKey(_dir));
    }

    [Fact]
    public void ApiKey_EnvironmentBeatsFile()
    {
        File.WriteAllText(Path.Combine(_dir, ".env"), "YANDEX_WEATHER_API_KEY=file-key\n");
        Environment.SetEnvironmentVariable(AppConfig.ApiKeyVariable, " env-key ");
        Assert.Equal("env-key", AppConfig.ResolveApiKey(_dir));
    }

    [Fact]
    public void Catalog_Has24CitiesAndSearchWorks()
    {
        Assert.Equal(24, CityCatalog.All.Count);
        Assert.Equal("Москва", CityCatalog.Search("мос")[0].Name);
        Assert.Equal("Сочи", CityCatalog.Search("СОЧ")[0].Name);
        Assert.Empty(CityCatalog.Search("zzzz"));
        Assert.Equal(24, CityCatalog.Search("  ").Count);
        Assert.Equal("Казань", CityCatalog.Find("казань")?.Name);
    }

    [Fact]
    public void Catalog_SearchPrefixesComeFirst()
    {
        var result = CityCatalog.Search("но");
        Assert.StartsWith("Но", result[0].Name);
    }

    [Fact]
    public void Cache_RoundTripsAndExpires()
    {
        var cache = new WeatherCache(_dir);
        var city = CityCatalog.Default;
        var saved = new DateTime(2026, 1, 1, 12, 0, 0);
        cache.Save(city, new WeatherResponse { Fact = new FactData { Temp = 7 } }, saved);

        var fresh = cache.Load(city, saved.AddHours(1));
        Assert.Equal(7, fresh!.Data!.Fact!.Temp);
        Assert.Null(cache.Load(city, saved.AddHours(25)));
        Assert.Null(cache.Load(CityCatalog.All[1], saved));
    }

    [Fact]
    public void Cache_IgnoresCorruptedFile()
    {
        var cache = new WeatherCache(_dir);
        var city = CityCatalog.Default;
        cache.Save(city, new WeatherResponse { Fact = new FactData() });
        foreach (var file in Directory.GetFiles(_dir, "*.json"))
        {
            File.WriteAllText(file, "{broken");
        }

        Assert.Null(cache.Load(city));
    }

    [Fact]
    public void Settings_RoundTripAndRecoverFromCorruption()
    {
        var path = Path.Combine(_dir, "settings.json");
        var store = new SettingsStore(path);
        Assert.Equal(CityCatalog.Default.Name, store.Load().City);

        store.Save(new AppSettings { City = "Сочи" });
        Assert.Equal("Сочи", store.Load().City);

        File.WriteAllText(path, "garbage");
        Assert.Equal(CityCatalog.Default.Name, store.Load().City);
    }

    [Theory]
    [InlineData(5.4, "+5°")]
    [InlineData(-3.2, "−3°")]
    [InlineData(0.2, "0°")]
    [InlineData(-0.4, "0°")]
    public void Formatting_Temperature(double value, string expected)
    {
        Assert.Equal(expected, Formatting.Temperature(value));
    }

    [Fact]
    public void Hourly_SelectsSortedUniqueFromNow()
    {
        var response = new WeatherResponse
        {
            Forecasts = new List<ForecastDay>
            {
                new() { Hours = new List<HourData> { new() { HourTs = 7200 }, new() { HourTs = 0 }, new() { HourTs = 3600 } } },
                new() { Hours = new List<HourData> { new() { HourTs = 7200 }, new() { HourTs = 10800 } } }
            }
        };

        var result = HourlySelector.Next24(response, 3600);
        Assert.Equal(new long[] { 3600, 7200, 10800 }, result.Select(h => h.HourTs).ToArray());
    }
}
