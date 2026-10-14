namespace WeatherScope.Services;

public enum UvRisk
{
    Low,
    Moderate,
    High,
    VeryHigh,
    Extreme
}

public static class UvLevel
{
    public static UvRisk Classify(double index)
    {
        if (index < 3)
        {
            return UvRisk.Low;
        }

        if (index < 6)
        {
            return UvRisk.Moderate;
        }

        if (index < 8)
        {
            return UvRisk.High;
        }

        if (index < 11)
        {
            return UvRisk.VeryHigh;
        }

        return UvRisk.Extreme;
    }

    public static string Label(UvRisk risk)
    {
        return risk switch
        {
            UvRisk.Low => "Низкий",
            UvRisk.Moderate => "Умеренный",
            UvRisk.High => "Высокий",
            UvRisk.VeryHigh => "Очень высокий",
            _ => "Экстремальный"
        };
    }

    public static string Advice(UvRisk risk)
    {
        return risk switch
        {
            UvRisk.Low => "Защита не требуется.",
            UvRisk.Moderate => "Желательны солнцезащитные очки и тень в полдень.",
            UvRisk.High => "Нанесите SPF 30 и наденьте головной убор.",
            UvRisk.VeryHigh => "Нанесите SPF 50, избегайте солнца с 11 до 16 часов.",
            _ => "Не выходите на открытое солнце. SPF 50 и закрытая одежда обязательны."
        };
    }
}
