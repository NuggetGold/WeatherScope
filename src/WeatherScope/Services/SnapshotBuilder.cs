using WeatherScope.Models;

namespace WeatherScope.Services;

public static class SnapshotBuilder
{
    private static readonly string[] DayNames =
    {
        "Воскресенье", "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота"
    };

    public static WeatherSnapshot Build(WeatherResponse response)
    {
        var fact = response.Fact ?? throw new WeatherException(
            WeatherErrorKind.EmptyResponse, "В ответе сервиса нет данных о текущей погоде.");

        var offset = response.Info?.TzInfo?.Offset ?? 0;
        var now = response.Now > 0 ? response.Now : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hours = HourlySelector.Next24(response, now);
        var pressures = hours.Take(4).Select(h => h.PressureMm).ToList();
        var trend = WeatherMath.PressureTrendOf(pressures);
        var firstDay = response.Forecasts.FirstOrDefault();
        var arrow = WindDirection.IsCalm(fact.WindDir) ? null : WindDirection.ArrowDegrees(fact.WindDir);
        var uv = fact.UvIndex;
        var risk = uv is null ? (UvRisk?)null : UvLevel.Classify(uv.Value);
        var (mode, intensity) = ResolveParticles(fact);

        return new WeatherSnapshot
        {
            Temp = fact.Temp,
            FeelsLike = fact.FeelsLike,
            TempText = Formatting.Temperature(fact.Temp),
            FeelsLikeText = $"Ощущается как {Formatting.Temperature(fact.FeelsLike)}",
            ConditionText = WeatherMath.ConditionText(fact.Condition),
            Accent = AccentPalette.ForTemperature(fact.Temp),
            WindSpeed = fact.WindSpeed,
            WindGust = fact.WindGust,
            WindArrowDegrees = arrow,
            WindCalm = WindDirection.IsCalm(fact.WindDir),
            WindDirText = WindDirection.Name(fact.WindDir),
            WindSummary = BuildWindSummary(fact),
            Pressure = fact.PressureMm,
            PressureText = Formatting.Pressure(fact.PressureMm),
            PressureTrendText = WeatherMath.PressureTrendText(trend),
            UvIndex = uv,
            UvLabel = risk is null ? "Нет данных" : UvLevel.Label(risk.Value),
            UvAdvice = risk is null ? string.Empty : UvLevel.Advice(risk.Value),
            SmartTip = SmartTipService.Build(fact, hours),
            PrecipitationOutlook = WeatherMath.PrecipitationOutlook(hours),
            Hourly = hours.Select(h => ToChartPoint(h, offset)).ToList(),
            Daily = response.Forecasts.Select(day => ToDaily(day, offset)).ToList(),
            Details = BuildDetails(fact, firstDay),
            ParticleMode = mode,
            ParticleIntensity = intensity,
            WindDrift = ResolveDrift(fact),
            IsThunder = fact.IsThunder
        };
    }

    private static string BuildWindSummary(FactData fact)
    {
        var text = Formatting.WindSpeed(fact.WindSpeed);
        if (fact.WindGust is { } gust && gust > 0)
        {
            text += $", порывы до {Formatting.WindSpeed(gust)}";
        }

        return text;
    }

    private static ChartPoint ToChartPoint(HourData hour, int offset)
    {
        var detail = $"{Formatting.Temperature(hour.Temp)}, ветер {Formatting.WindSpeed(hour.WindSpeed)}, " +
                     $"осадки {Formatting.Number(hour.PrecMm)} мм";
        return new ChartPoint(HourlySelector.Label(hour, offset), hour.Temp, hour.PrecMm, detail);
    }

    public static string DayLabel(ForecastDay day, int offset)
    {
        if (DateTime.TryParseExact(day.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed))
        {
            return DayNames[(int)parsed.DayOfWeek];
        }

        if (day.DateTs > 0)
        {
            return DayNames[(int)DateTimeOffset.FromUnixTimeSeconds(day.DateTs + offset).UtcDateTime.DayOfWeek];
        }

        return day.Date ?? "—";
    }

    private static DailyItem ToDaily(ForecastDay day, int offset)
    {
        var parts = day.Parts;
        var dayPart = parts?.DayShort ?? parts?.Day;
        var nightPart = parts?.NightShort ?? parts?.Night;
        var label = DayLabel(day, offset);
        var dayTemp = dayPart?.Temp ?? dayPart?.TempAvg ?? dayPart?.TempMax;
        var nightTemp = nightPart?.Temp ?? nightPart?.TempAvg ?? nightPart?.TempMin;
        var precip = dayPart?.PrecMm is { } mm ? $"{Formatting.Number(mm)} мм" : "—";
        var wind = dayPart?.WindSpeed is { } speed ? Formatting.WindSpeed(speed) : "—";

        return new DailyItem(
            label,
            WeatherMath.ConditionText(dayPart?.Condition),
            dayTemp is { } d ? Formatting.Temperature(d) : "—",
            nightTemp is { } n ? Formatting.Temperature(n) : "—",
            precip,
            wind);
    }

    private static IReadOnlyList<DetailItem> BuildDetails(FactData fact, ForecastDay? day)
    {
        var items = new List<DetailItem>
        {
            new("Влажность", $"{Formatting.Number(fact.Humidity, "0")} %"),
            new("Точка росы", Formatting.Temperature(WeatherMath.DewPoint(fact.Temp, fact.Humidity))),
            new("Ветрохолод", Formatting.Temperature(WeatherMath.WindChill(fact.Temp, fact.WindSpeed))),
            new("Осадки", WeatherMath.PrecipitationTypeText(fact.PrecType))
        };

        if (fact.Visibility is { } visibility)
        {
            items.Add(new DetailItem("Видимость",
                $"{Formatting.Number(WeatherMath.VisibilityKilometers(visibility))} км"));
        }

        if (fact.TempWater is { } water)
        {
            items.Add(new DetailItem("Вода", Formatting.Temperature(water)));
        }

        if (day is not null)
        {
            if (!string.IsNullOrEmpty(day.Sunrise))
            {
                items.Add(new DetailItem("Восход", day.Sunrise));
            }

            if (!string.IsNullOrEmpty(day.Sunset))
            {
                items.Add(new DetailItem("Закат", day.Sunset));
            }

            items.Add(new DetailItem("Фаза Луны", WeatherMath.MoonPhaseName(day.MoonCode)));
        }

        return items;
    }

    private static (ParticleMode Mode, double Intensity) ResolveParticles(FactData fact)
    {
        if (fact.IsThunder)
        {
            return (ParticleMode.Storm, 1.0);
        }

        var strength = Math.Clamp(fact.PrecStrength <= 0 ? 0.5 : fact.PrecStrength, 0.2, 1.0);
        if (fact.PrecType is 3 or 2)
        {
            return (ParticleMode.Snow, strength);
        }

        if (fact.PrecType is 1 or 4)
        {
            return (ParticleMode.Rain, strength);
        }

        if (fact.Cloudness is { } cloud && cloud >= 0.5)
        {
            return (ParticleMode.Clouds, cloud);
        }

        return (ParticleMode.None, 0);
    }

    private static double ResolveDrift(FactData fact)
    {
        var arrow = WindDirection.ArrowDegrees(fact.WindDir);
        if (arrow is null)
        {
            return 0;
        }

        var radians = arrow.Value * Math.PI / 180.0;
        var horizontal = Math.Sin(radians);
        return Math.Clamp(horizontal * fact.WindSpeed / 10.0, -1.0, 1.0);
    }
}
