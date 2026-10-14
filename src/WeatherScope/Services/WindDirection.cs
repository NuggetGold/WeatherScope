namespace WeatherScope.Services;

public static class WindDirection
{
    private static readonly Dictionary<string, double> Degrees = new(StringComparer.OrdinalIgnoreCase)
    {
        ["n"] = 0,
        ["ne"] = 45,
        ["e"] = 90,
        ["se"] = 135,
        ["s"] = 180,
        ["sw"] = 225,
        ["w"] = 270,
        ["nw"] = 315
    };

    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["n"] = "северный",
        ["ne"] = "северо-восточный",
        ["e"] = "восточный",
        ["se"] = "юго-восточный",
        ["s"] = "южный",
        ["sw"] = "юго-западный",
        ["w"] = "западный",
        ["nw"] = "северо-западный",
        ["c"] = "штиль"
    };

    private static readonly Dictionary<string, string> Short = new(StringComparer.OrdinalIgnoreCase)
    {
        ["n"] = "С",
        ["ne"] = "СВ",
        ["e"] = "В",
        ["se"] = "ЮВ",
        ["s"] = "Ю",
        ["sw"] = "ЮЗ",
        ["w"] = "З",
        ["nw"] = "СЗ",
        ["c"] = "—"
    };

    public static bool IsCalm(string? code)
    {
        return string.Equals(code, "c", StringComparison.OrdinalIgnoreCase);
    }

    public static double? FromDegrees(string? code)
    {
        if (code is null)
        {
            return null;
        }

        return Degrees.TryGetValue(code, out var value) ? value : null;
    }

    public static double? ArrowDegrees(string? code)
    {
        var from = FromDegrees(code);
        if (from is null)
        {
            return null;
        }

        return (from.Value + 180) % 360;
    }

    public static string Name(string? code)
    {
        if (code is not null && Names.TryGetValue(code, out var name))
        {
            return name;
        }

        return "нет данных";
    }

    public static string ShortName(string? code)
    {
        if (code is not null && Short.TryGetValue(code, out var name))
        {
            return name;
        }

        return "—";
    }
}
