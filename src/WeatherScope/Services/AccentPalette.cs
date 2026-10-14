using System.Windows.Media;

namespace WeatherScope.Services;

public static class AccentPalette
{
    private static readonly (double Temperature, Color Color)[] Stops =
    {
        (-15, Color.FromRgb(0x1E, 0x90, 0xFF)),
        (-5, Color.FromRgb(0x46, 0xB4, 0xDC)),
        (7.5, Color.FromRgb(0x4E, 0xCD, 0xC4)),
        (20, Color.FromRgb(0xA8, 0xE0, 0x63)),
        (30, Color.FromRgb(0xFF, 0x9A, 0x3C)),
        (40, Color.FromRgb(0xFF, 0x47, 0x57))
    };

    public static Color ForTemperature(double temperature)
    {
        if (temperature <= Stops[0].Temperature)
        {
            return Stops[0].Color;
        }

        if (temperature >= Stops[^1].Temperature)
        {
            return Stops[^1].Color;
        }

        for (var i = 0; i < Stops.Length - 1; i++)
        {
            var low = Stops[i];
            var high = Stops[i + 1];
            if (temperature >= low.Temperature && temperature <= high.Temperature)
            {
                var ratio = (temperature - low.Temperature) / (high.Temperature - low.Temperature);
                return Blend(low.Color, high.Color, ratio);
            }
        }

        return Stops[^1].Color;
    }

    public static Color Blend(Color from, Color to, double ratio)
    {
        var t = Math.Clamp(ratio, 0, 1);
        return Color.FromRgb(
            (byte)Math.Round(from.R + (to.R - from.R) * t),
            (byte)Math.Round(from.G + (to.G - from.G) * t),
            (byte)Math.Round(from.B + (to.B - from.B) * t));
    }
}
