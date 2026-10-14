using System.Globalization;

namespace WeatherScope.Services;

public static class Formatting
{
    public static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public static string Temperature(double value)
    {
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded > 0)
        {
            return $"+{rounded}°";
        }

        return rounded < 0 ? $"−{Math.Abs(rounded)}°" : "0°";
    }

    public static string Number(double value, string format = "0.#")
    {
        return value.ToString(format, Ru);
    }

    public static string WindSpeed(double value)
    {
        return $"{Number(value)} м/с";
    }

    public static string Pressure(double value)
    {
        return $"{Number(value, "0")} мм рт. ст.";
    }
}
