using WeatherScope.Models;
using WeatherScope.Services;
using Xunit;

namespace WeatherScope.Tests;

public class WeatherMathTests
{
    [Theory]
    [InlineData(20, 100, 20)]
    [InlineData(25, 50, 13.9)]
    [InlineData(0, 80, -3.0)]
    public void DewPoint_MatchesMagnusReference(double temp, double humidity, double expected)
    {
        Assert.Equal(expected, WeatherMath.DewPoint(temp, humidity), 1);
    }

    [Fact]
    public void DewPoint_ClampsZeroHumidity()
    {
        var result = WeatherMath.DewPoint(10, 0);
        Assert.False(double.IsNaN(result) || double.IsInfinity(result));
    }

    [Fact]
    public void WindChill_ReturnsTemperature_WhenWarm()
    {
        Assert.Equal(15, WeatherMath.WindChill(15, 10));
    }

    [Fact]
    public void WindChill_ReturnsTemperature_WhenWindTooWeak()
    {
        Assert.Equal(-5, WeatherMath.WindChill(-5, 1));
    }

    [Fact]
    public void WindChill_MatchesCanadianFormula()
    {
        Assert.Equal(-20.3, WeatherMath.WindChill(-10, 10), 1);
    }

    [Fact]
    public void PressureTrend_Unknown_WhenFewPoints()
    {
        Assert.Equal(PressureTrend.Unknown, WeatherMath.PressureTrendOf(new[] { 750.0, 751.0 }));
    }

    [Theory]
    [InlineData(750, 750, 750.5, PressureTrend.Stable)]
    [InlineData(750, 752, 754, PressureTrend.Rising)]
    [InlineData(754, 752, 750, PressureTrend.Falling)]
    public void PressureTrend_ClassifiesDelta(double a, double b, double c, PressureTrend expected)
    {
        Assert.Equal(expected, WeatherMath.PressureTrendOf(new[] { a, b, c }));
    }

    [Fact]
    public void PrecipitationOutlook_Dry()
    {
        var hours = Enumerable.Range(0, 5).Select(_ => new HourData()).ToList();
        Assert.Equal("без осадков ближайшие 5 ч", WeatherMath.PrecipitationOutlook(hours));
    }

    [Fact]
    public void PrecipitationOutlook_StartsLater()
    {
        var hours = new List<HourData> { new(), new(), new() { PrecMm = 1.2 } };
        Assert.Equal("осадки начнутся через 2 ч", WeatherMath.PrecipitationOutlook(hours));
    }

    [Fact]
    public void PrecipitationOutlook_EndsLater()
    {
        var hours = new List<HourData> { new() { PrecMm = 1 }, new() { PrecMm = 1 }, new() };
        Assert.Equal("осадки закончатся через 2 ч", WeatherMath.PrecipitationOutlook(hours));
    }

    [Fact]
    public void PrecipitationOutlook_Empty()
    {
        Assert.Equal("нет данных об осадках", WeatherMath.PrecipitationOutlook(new List<HourData>()));
    }

    [Theory]
    [InlineData(0, "Полнолуние")]
    [InlineData(2, "Убывающая луна")]
    [InlineData(4, "Последняя четверть")]
    [InlineData(8, "Новолуние")]
    [InlineData(10, "Растущая луна")]
    [InlineData(12, "Первая четверть")]
    [InlineData(15, "Растущая луна")]
    [InlineData(99, "Нет данных")]
    public void MoonPhase_Names(int code, string expected)
    {
        Assert.Equal(expected, WeatherMath.MoonPhaseName(code));
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(10000, 10)]
    public void Visibility_NormalizesToKilometers(double raw, double expected)
    {
        Assert.Equal(expected, WeatherMath.VisibilityKilometers(raw));
    }

    [Fact]
    public void ConditionText_UnknownFallsBack()
    {
        Assert.Equal("Нет данных", WeatherMath.ConditionText("unknown-code"));
        Assert.Equal("Ясно", WeatherMath.ConditionText("clear"));
    }
}
