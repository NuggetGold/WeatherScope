using System.Windows.Media;
using WeatherScope.Models;
using WeatherScope.Services;
using Xunit;

namespace WeatherScope.Tests;

public class PaletteAndTipTests
{
    [Theory]
    [InlineData(-30, 0x1E, 0x90, 0xFF)]
    [InlineData(-15, 0x1E, 0x90, 0xFF)]
    [InlineData(-5, 0x46, 0xB4, 0xDC)]
    [InlineData(20, 0xA8, 0xE0, 0x63)]
    [InlineData(30, 0xFF, 0x9A, 0x3C)]
    [InlineData(40, 0xFF, 0x47, 0x57)]
    [InlineData(60, 0xFF, 0x47, 0x57)]
    public void Accent_HitsAnchors(double temp, byte r, byte g, byte b)
    {
        Assert.Equal(Color.FromRgb(r, g, b), AccentPalette.ForTemperature(temp));
    }

    [Fact]
    public void Accent_InterpolatesBetweenStops()
    {
        var mid = AccentPalette.ForTemperature(-10);
        Assert.InRange(mid.R, 0x1E, 0x46);
        Assert.InRange(mid.B, 0xDC, 0xFF);
    }

    [Fact]
    public void Accent_ChangesMonotonicallyWithoutExceptions()
    {
        for (var t = -45; t <= 55; t++)
        {
            var color = AccentPalette.ForTemperature(t);
            Assert.Equal(255, color.A);
        }
    }

    [Fact]
    public void Tip_NullFact()
    {
        Assert.Equal("Нет данных для рекомендаций.", SmartTipService.Build(null));
    }

    [Fact]
    public void Tip_ThunderHasPriority()
    {
        var fact = new FactData { IsThunder = true, UvIndex = 12, WindSpeed = 20 };
        Assert.Contains("Гроза", SmartTipService.Build(fact));
    }

    [Fact]
    public void Tip_ExtremeUv()
    {
        var fact = new FactData { UvIndex = 11 };
        Assert.Contains("Экстремальный UV", SmartTipService.Build(fact));
    }

    [Fact]
    public void Tip_Precipitation()
    {
        var fact = new FactData { PrecType = 1, PrecStrength = 0.5 };
        Assert.Contains("зонт", SmartTipService.Build(fact));
    }

    [Fact]
    public void Tip_PrecipitationFromHours()
    {
        var fact = new FactData();
        var hours = new List<HourData> { new(), new() { PrecMm = 2 } };
        Assert.Contains("зонт", SmartTipService.Build(fact, hours));
    }

    [Fact]
    public void Tip_StrongWindFromGust()
    {
        var fact = new FactData { WindSpeed = 8, WindGust = 18 };
        Assert.Contains("Сильный ветер", SmartTipService.Build(fact));
    }

    [Fact]
    public void Tip_Comfortable()
    {
        var fact = new FactData { Temp = 18, FeelsLike = 18, Humidity = 40, WindSpeed = 2 };
        Assert.Contains("комфортная", SmartTipService.Build(fact));
    }
}
