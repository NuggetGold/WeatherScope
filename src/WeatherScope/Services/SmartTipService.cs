using WeatherScope.Models;

namespace WeatherScope.Services;

public static class SmartTipService
{
    public static string Build(FactData? fact, IReadOnlyList<HourData>? hours = null)
    {
        if (fact is null)
        {
            return "Нет данных для рекомендаций.";
        }

        if (fact.IsThunder)
        {
            return "⚡ Гроза! Оставайтесь в помещении.";
        }

        if (fact.UvIndex is { } uv && uv >= 11)
        {
            return "☀️ Экстремальный UV! Нанесите SPF 50 и носите головной убор.";
        }

        var gust = Math.Max(fact.WindSpeed, fact.WindGust ?? 0);
        if (gust >= 15)
        {
            return "💨 Сильный ветер. Будьте осторожны.";
        }

        if (fact.PrecType > 0 || fact.PrecStrength > 0)
        {
            return "☔ Ожидаются осадки. Возьмите зонт!";
        }

        if (hours is not null && hours.Take(6).Any(WeatherMath.HasPrecipitation))
        {
            return "☔ Ожидаются осадки. Возьмите зонт!";
        }

        if (fact.Temp <= 10 && WeatherMath.WindChill(fact.Temp, fact.WindSpeed) <= -20)
        {
            return "🥶 Сильный ветрохолод. Закройте лицо и руки.";
        }

        if (fact.FeelsLike <= -25)
        {
            return "🥶 Сильный мороз. Оденьтесь теплее и ограничьте время на улице.";
        }

        if (fact.FeelsLike >= 32)
        {
            return "🥵 Жара. Пейте больше воды и избегайте солнца днём.";
        }

        if (fact.UvIndex is { } high && high >= 6)
        {
            return "🕶️ Высокий UV. Используйте солнцезащитный крем.";
        }

        if (fact.Visibility is { } visibility && WeatherMath.VisibilityKilometers(visibility) < 1)
        {
            return "🌫️ Плохая видимость. Двигайтесь осторожно.";
        }

        if (WeatherMath.DewPoint(fact.Temp, fact.Humidity) >= 16)
        {
            return "💧 Душно и влажно. Проветривайте помещения.";
        }

        return "🌤️ Погода комфортная. Хорошего дня!";
    }
}
