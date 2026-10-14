using Newtonsoft.Json;
using WeatherScope.Models;

namespace WeatherScope.Services;

public sealed class CachedWeather
{
    [JsonProperty("saved_at")] public DateTime SavedAt { get; set; }
    [JsonProperty("city")] public string City { get; set; } = string.Empty;
    [JsonProperty("data")] public WeatherResponse? Data { get; set; }
}

public sealed class WeatherCache
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);
    private readonly string _directory;

    public WeatherCache(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WeatherScope",
            "cache");
    }

    public void Save(City city, WeatherResponse data, DateTime? now = null)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var entry = new CachedWeather
            {
                SavedAt = now ?? DateTime.Now,
                City = city.Name,
                Data = data
            };
            File.WriteAllText(PathFor(city), JsonConvert.SerializeObject(entry));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public CachedWeather? Load(City city, DateTime? now = null)
    {
        var path = PathFor(city);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var entry = JsonConvert.DeserializeObject<CachedWeather>(File.ReadAllText(path));
            if (entry?.Data is null)
            {
                return null;
            }

            var current = now ?? DateTime.Now;
            return current - entry.SavedAt > MaxAge ? null : entry;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private string PathFor(City city)
    {
        var name = $"{city.Lat:F2}_{city.Lon:F2}".Replace(',', '.');
        return Path.Combine(_directory, $"{name}.json");
    }
}
