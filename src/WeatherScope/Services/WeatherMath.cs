using WeatherScope.Models;

namespace WeatherScope.Services;

public enum PressureTrend
{
    Unknown,
    Falling,
    Stable,
    Rising
}

public static class WeatherMath
{
    private const double MagnusA = 17.62;
    private const double MagnusB = 243.12;

    public static double DewPoint(double temperature, double humidity)
    {
        var rh = Math.Clamp(humidity, 1, 100);
        var gamma = Math.Log(rh / 100.0) + MagnusA * temperature / (MagnusB + temperature);
        return MagnusB * gamma / (MagnusA - gamma);
    }

    public static double WindChill(double temperature, double windSpeedMetersPerSecond)
    {
        var kmh = windSpeedMetersPerSecond * 3.6;
        if (temperature > 10 || kmh < 4.8)
        {
            return temperature;
        }

        var factor = Math.Pow(kmh, 0.16);
        return 13.12 + 0.6215 * temperature - 11.37 * factor + 0.3965 * temperature * factor;
    }

    public static PressureTrend PressureTrendOf(IReadOnlyList<double> pressures)
    {
        if (pressures.Count < 3)
        {
            return PressureTrend.Unknown;
        }

        var delta = pressures[^1] - pressures[0];
        if (Math.Abs(delta) < 1)
        {
            return PressureTrend.Stable;
        }

        return delta > 0 ? PressureTrend.Rising : PressureTrend.Falling;
    }

    public static string PressureTrendText(PressureTrend trend)
    {
        return trend switch
        {
            PressureTrend.Falling => "давление падает",
            PressureTrend.Rising => "давление растёт",
            PressureTrend.Stable => "давление стабильно",
            _ => "тренд неизвестен"
        };
    }

    public static bool HasPrecipitation(HourData hour)
    {
        return hour.PrecMm > 0 || hour.PrecStrength > 0 || hour.PrecType > 0;
    }

    public static string PrecipitationOutlook(IReadOnlyList<HourData> hours)
    {
        if (hours.Count == 0)
        {
            return "нет данных об осадках";
        }

        var firstWet = -1;
        for (var i = 0; i < hours.Count; i++)
        {
            if (HasPrecipitation(hours[i]))
            {
                firstWet = i;
                break;
            }
        }

        if (firstWet < 0)
        {
            return $"без осадков ближайшие {hours.Count} ч";
        }

        if (firstWet == 0)
        {
            var lastWet = 0;
            while (lastWet + 1 < hours.Count && HasPrecipitation(hours[lastWet + 1]))
            {
                lastWet++;
            }

            if (lastWet + 1 >= hours.Count)
            {
                return "осадки продолжатся весь день";
            }

            return $"осадки закончатся через {lastWet + 1} ч";
        }

        return $"осадки начнутся через {firstWet} ч";
    }

    public static double VisibilityKilometers(double raw)
    {
        return raw >= 100 ? raw / 1000.0 : raw;
    }

    public static string MoonPhaseName(int code)
    {
        return code switch
        {
            0 => "Полнолуние",
            >= 1 and <= 3 => "Убывающая луна",
            4 => "Последняя четверть",
            >= 5 and <= 7 => "Убывающая луна",
            8 => "Новолуние",
            >= 9 and <= 11 => "Растущая луна",
            12 => "Первая четверть",
            >= 13 and <= 15 => "Растущая луна",
            _ => "Нет данных"
        };
    }

    public static string ConditionText(string? condition)
    {
        return condition?.ToLowerInvariant() switch
        {
            "clear" => "Ясно",
            "partly-cloudy" => "Малооблачно",
            "cloudy" => "Облачно с прояснениями",
            "overcast" => "Пасмурно",
            "light-rain" => "Небольшой дождь",
            "rain" => "Дождь",
            "heavy-rain" => "Сильный дождь",
            "showers" => "Ливень",
            "wet-snow" => "Дождь со снегом",
            "light-snow" => "Небольшой снег",
            "snow" => "Снег",
            "snow-showers" => "Снегопад",
            "hail" => "Град",
            "thunderstorm" => "Гроза",
            "thunderstorm-with-rain" => "Дождь с грозой",
            "thunderstorm-with-hail" => "Гроза с градом",
            "partly-cloudy-and-light-rain" => "Малооблачно, небольшой дождь",
            "partly-cloudy-and-rain" => "Малооблачно, дождь",
            "overcast-and-rain" => "Пасмурно, дождь",
            "overcast-thunderstorms-with-rain" => "Пасмурно, дождь с грозой",
            "cloudy-and-light-rain" => "Облачно, небольшой дождь",
            "overcast-and-light-rain" => "Пасмурно, небольшой дождь",
            "cloudy-and-rain" => "Облачно, дождь",
            "overcast-and-wet-snow" => "Пасмурно, дождь со снегом",
            "partly-cloudy-and-light-snow" => "Малооблачно, небольшой снег",
            "partly-cloudy-and-snow" => "Малооблачно, снег",
            "overcast-and-snow" => "Пасмурно, снег",
            "cloudy-and-light-snow" => "Облачно, небольшой снег",
            "overcast-and-light-snow" => "Пасмурно, небольшой снег",
            "cloudy-and-snow" => "Облачно, снег",
            _ => "Нет данных"
        };
    }

    public static string PrecipitationTypeText(int type)
    {
        return type switch
        {
            1 => "дождь",
            2 => "дождь со снегом",
            3 => "снег",
            4 => "град",
            _ => "без осадков"
        };
    }
}
