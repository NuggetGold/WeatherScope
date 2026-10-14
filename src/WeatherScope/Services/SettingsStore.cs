using Newtonsoft.Json;

namespace WeatherScope.Services;

public sealed class AppSettings
{
    [JsonProperty("version")] public int Version { get; set; } = 1;
    [JsonProperty("city")] public string City { get; set; } = CityCatalog.Default.Name;
}

public sealed class SettingsStore
{
    private readonly string _path;

    public SettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WeatherScope",
            "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            return JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
