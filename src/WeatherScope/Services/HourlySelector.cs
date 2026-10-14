using WeatherScope.Models;

namespace WeatherScope.Services;

public static class HourlySelector
{
    public static IReadOnlyList<HourData> Next24(WeatherResponse response, long nowTimestamp)
    {
        return response.Forecasts
            .SelectMany(day => day.Hours)
            .Where(hour => hour.HourTs > 0)
            .GroupBy(hour => hour.HourTs)
            .Select(group => group.First())
            .OrderBy(hour => hour.HourTs)
            .Where(hour => hour.HourTs >= nowTimestamp - 3599)
            .Take(24)
            .ToList();
    }

    public static string Label(HourData hour, int utcOffsetSeconds)
    {
        var local = DateTimeOffset.FromUnixTimeSeconds(hour.HourTs + utcOffsetSeconds);
        return $"{local.Hour:00}:00";
    }
}
