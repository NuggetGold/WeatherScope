using WeatherScope.Models;

namespace WeatherScope.Services;

public static class CityCatalog
{
    public static IReadOnlyList<City> All { get; } = new List<City>
    {
        new("Москва", 55.7558, 37.6173),
        new("Санкт-Петербург", 59.9343, 30.3351),
        new("Новосибирск", 55.0084, 82.9357),
        new("Екатеринбург", 56.8389, 60.6057),
        new("Казань", 55.7887, 49.1221),
        new("Нижний Новгород", 56.2965, 43.9361),
        new("Челябинск", 55.1644, 61.4368),
        new("Самара", 53.1959, 50.1002),
        new("Омск", 54.9885, 73.3242),
        new("Ростов-на-Дону", 47.2357, 39.7015),
        new("Уфа", 54.7388, 55.9721),
        new("Красноярск", 56.0153, 92.8932),
        new("Воронеж", 51.6720, 39.1843),
        new("Пермь", 58.0105, 56.2502),
        new("Волгоград", 48.7080, 44.5133),
        new("Краснодар", 45.0355, 38.9753),
        new("Сочи", 43.5855, 39.7231),
        new("Владивосток", 43.1155, 131.8855),
        new("Калининград", 54.7104, 20.4522),
        new("Мурманск", 68.9585, 33.0827),
        new("Якутск", 62.0355, 129.6755),
        new("Минск", 53.9006, 27.5590),
        new("Алматы", 43.2220, 76.8512),
        new("Астана", 51.1605, 71.4704)
    };

    public static City Default => All[0];

    public static IReadOnlyList<City> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return All;
        }

        var text = query.Trim();
        var prefix = All.Where(c => c.Name.StartsWith(text, StringComparison.CurrentCultureIgnoreCase));
        var inner = All.Where(c => c.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase));
        return prefix.Concat(inner).Distinct().ToList();
    }

    public static City? Find(string? name)
    {
        return All.FirstOrDefault(c => string.Equals(c.Name, name?.Trim(), StringComparison.CurrentCultureIgnoreCase));
    }
}
