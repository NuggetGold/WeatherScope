using Newtonsoft.Json;

namespace WeatherScope.Models;

public sealed class WeatherResponse
{
    [JsonProperty("now")] public long Now { get; set; }
    [JsonProperty("now_dt")] public string? NowDt { get; set; }
    [JsonProperty("info")] public LocationInfo? Info { get; set; }
    [JsonProperty("fact")] public FactData? Fact { get; set; }
    [JsonProperty("forecasts")] public List<ForecastDay> Forecasts { get; set; } = new();
}

public sealed class LocationInfo
{
    [JsonProperty("lat")] public double Lat { get; set; }
    [JsonProperty("lon")] public double Lon { get; set; }
    [JsonProperty("tzinfo")] public TimeZoneInfoData? TzInfo { get; set; }
}

public sealed class TimeZoneInfoData
{
    [JsonProperty("offset")] public int Offset { get; set; }
    [JsonProperty("name")] public string? Name { get; set; }
}

public sealed class FactData
{
    [JsonProperty("temp")] public double Temp { get; set; }
    [JsonProperty("feels_like")] public double FeelsLike { get; set; }
    [JsonProperty("temp_water")] public double? TempWater { get; set; }
    [JsonProperty("condition")] public string? Condition { get; set; }
    [JsonProperty("wind_speed")] public double WindSpeed { get; set; }
    [JsonProperty("wind_gust")] public double? WindGust { get; set; }
    [JsonProperty("wind_dir")] public string? WindDir { get; set; }
    [JsonProperty("pressure_mm")] public double PressureMm { get; set; }
    [JsonProperty("humidity")] public double Humidity { get; set; }
    [JsonProperty("uv_index")] public double? UvIndex { get; set; }
    [JsonProperty("visibility")] public double? Visibility { get; set; }
    [JsonProperty("prec_type")] public int PrecType { get; set; }
    [JsonProperty("prec_strength")] public double PrecStrength { get; set; }
    [JsonProperty("is_thunder")] public bool IsThunder { get; set; }
    [JsonProperty("cloudness")] public double? Cloudness { get; set; }
    [JsonProperty("daytime")] public string? Daytime { get; set; }
    [JsonProperty("obs_time")] public long ObsTime { get; set; }
}

public sealed class ForecastDay
{
    [JsonProperty("date")] public string? Date { get; set; }
    [JsonProperty("date_ts")] public long DateTs { get; set; }
    [JsonProperty("sunrise")] public string? Sunrise { get; set; }
    [JsonProperty("sunset")] public string? Sunset { get; set; }
    [JsonProperty("moon_code")] public int MoonCode { get; set; }
    [JsonProperty("moon_text")] public string? MoonText { get; set; }
    [JsonProperty("parts")] public DayParts? Parts { get; set; }
    [JsonProperty("hours")] public List<HourData> Hours { get; set; } = new();
}

public sealed class DayParts
{
    [JsonProperty("morning")] public DayPart? Morning { get; set; }
    [JsonProperty("day")] public DayPart? Day { get; set; }
    [JsonProperty("evening")] public DayPart? Evening { get; set; }
    [JsonProperty("night")] public DayPart? Night { get; set; }
    [JsonProperty("day_short")] public DayPart? DayShort { get; set; }
    [JsonProperty("night_short")] public DayPart? NightShort { get; set; }
}

public sealed class DayPart
{
    [JsonProperty("temp")] public double? Temp { get; set; }
    [JsonProperty("temp_min")] public double? TempMin { get; set; }
    [JsonProperty("temp_max")] public double? TempMax { get; set; }
    [JsonProperty("temp_avg")] public double? TempAvg { get; set; }
    [JsonProperty("feels_like")] public double? FeelsLike { get; set; }
    [JsonProperty("condition")] public string? Condition { get; set; }
    [JsonProperty("wind_speed")] public double? WindSpeed { get; set; }
    [JsonProperty("wind_gust")] public double? WindGust { get; set; }
    [JsonProperty("wind_dir")] public string? WindDir { get; set; }
    [JsonProperty("pressure_mm")] public double? PressureMm { get; set; }
    [JsonProperty("humidity")] public double? Humidity { get; set; }
    [JsonProperty("prec_mm")] public double? PrecMm { get; set; }
    [JsonProperty("prec_type")] public int PrecType { get; set; }
    [JsonProperty("prec_strength")] public double PrecStrength { get; set; }
}

public sealed class HourData
{
    [JsonProperty("hour")] public string? Hour { get; set; }
    [JsonProperty("hour_ts")] public long HourTs { get; set; }
    [JsonProperty("temp")] public double Temp { get; set; }
    [JsonProperty("feels_like")] public double FeelsLike { get; set; }
    [JsonProperty("condition")] public string? Condition { get; set; }
    [JsonProperty("wind_speed")] public double WindSpeed { get; set; }
    [JsonProperty("wind_gust")] public double? WindGust { get; set; }
    [JsonProperty("wind_dir")] public string? WindDir { get; set; }
    [JsonProperty("pressure_mm")] public double PressureMm { get; set; }
    [JsonProperty("humidity")] public double Humidity { get; set; }
    [JsonProperty("prec_mm")] public double PrecMm { get; set; }
    [JsonProperty("prec_type")] public int PrecType { get; set; }
    [JsonProperty("prec_strength")] public double PrecStrength { get; set; }
}
