using WeatherScope.Services;
using Xunit;

namespace WeatherScope.Tests;

public class WindAndUvTests
{
    [Theory]
    [InlineData("n", 0)]
    [InlineData("ne", 45)]
    [InlineData("e", 90)]
    [InlineData("se", 135)]
    [InlineData("s", 180)]
    [InlineData("sw", 225)]
    [InlineData("w", 270)]
    [InlineData("nw", 315)]
    public void FromDegrees_MapsCodes(string code, double expected)
    {
        Assert.Equal(expected, WindDirection.FromDegrees(code));
    }

    [Theory]
    [InlineData("n", 180)]
    [InlineData("s", 0)]
    [InlineData("w", 90)]
    [InlineData("nw", 135)]
    public void ArrowDegrees_PointsWhereWindBlows(string code, double expected)
    {
        Assert.Equal(expected, WindDirection.ArrowDegrees(code));
    }

    [Fact]
    public void Calm_HasNoDirection()
    {
        Assert.True(WindDirection.IsCalm("c"));
        Assert.Null(WindDirection.FromDegrees("c"));
        Assert.Equal("штиль", WindDirection.Name("c"));
    }

    [Fact]
    public void Unknown_HasFallbacks()
    {
        Assert.Null(WindDirection.ArrowDegrees(null));
        Assert.Equal("нет данных", WindDirection.Name("zz"));
        Assert.Equal("—", WindDirection.ShortName(null));
    }

    [Theory]
    [InlineData(0, UvRisk.Low)]
    [InlineData(2.9, UvRisk.Low)]
    [InlineData(3, UvRisk.Moderate)]
    [InlineData(5, UvRisk.Moderate)]
    [InlineData(6, UvRisk.High)]
    [InlineData(7, UvRisk.High)]
    [InlineData(8, UvRisk.VeryHigh)]
    [InlineData(10, UvRisk.VeryHigh)]
    [InlineData(11, UvRisk.Extreme)]
    [InlineData(14, UvRisk.Extreme)]
    public void UvClassify_UsesWhoBoundaries(double index, UvRisk expected)
    {
        Assert.Equal(expected, UvLevel.Classify(index));
    }

    [Fact]
    public void UvLabels_AreRussian()
    {
        Assert.Equal("Низкий", UvLevel.Label(UvRisk.Low));
        Assert.Equal("Экстремальный", UvLevel.Label(UvRisk.Extreme));
        Assert.False(string.IsNullOrWhiteSpace(UvLevel.Advice(UvRisk.High)));
    }
}
