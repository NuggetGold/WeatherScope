using System.Windows.Media;

namespace WeatherScope.Models;

public enum ParticleMode
{
    None,
    Clouds,
    Rain,
    Snow,
    Storm
}

public sealed record ChartPoint(string Label, double Value, double PrecMm, string Detail);

public sealed record DailyItem(
    string DayLabel,
    string ConditionText,
    string DayTemp,
    string NightTemp,
    string Precipitation,
    string Wind);

public sealed record DetailItem(string Label, string Value);

public sealed class WeatherSnapshot
{
    public double Temp { get; init; }
    public double FeelsLike { get; init; }
    public string TempText { get; init; } = "—";
    public string FeelsLikeText { get; init; } = "—";
    public string ConditionText { get; init; } = "Нет данных";
    public Color Accent { get; init; }

    public double WindSpeed { get; init; }
    public double? WindGust { get; init; }
    public double? WindArrowDegrees { get; init; }
    public bool WindCalm { get; init; }
    public string WindDirText { get; init; } = "—";
    public string WindSummary { get; init; } = "—";

    public double Pressure { get; init; }
    public string PressureText { get; init; } = "—";
    public string PressureTrendText { get; init; } = "—";

    public double? UvIndex { get; init; }
    public string UvLabel { get; init; } = "Нет данных";
    public string UvAdvice { get; init; } = string.Empty;

    public string SmartTip { get; init; } = string.Empty;
    public string PrecipitationOutlook { get; init; } = string.Empty;

    public IReadOnlyList<ChartPoint> Hourly { get; init; } = Array.Empty<ChartPoint>();
    public IReadOnlyList<DailyItem> Daily { get; init; } = Array.Empty<DailyItem>();
    public IReadOnlyList<DetailItem> Details { get; init; } = Array.Empty<DetailItem>();

    public ParticleMode ParticleMode { get; init; }
    public double ParticleIntensity { get; init; }
    public double WindDrift { get; init; }
    public bool IsThunder { get; init; }
}
