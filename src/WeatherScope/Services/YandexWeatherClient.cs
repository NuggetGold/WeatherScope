using System.Globalization;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WeatherScope.Models;

namespace WeatherScope.Services;

public interface IWeatherClient
{
    Task<WeatherResponse> GetWeatherAsync(City city, CancellationToken cancellationToken);
}

public sealed class YandexWeatherClient : IWeatherClient
{
    private static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4)
    };

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public YandexWeatherClient(HttpClient http, string apiKey, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _http = http;
        _apiKey = apiKey;
        _delay = delay ?? Task.Delay;
    }

    public async Task<WeatherResponse> GetWeatherAsync(City city, CancellationToken cancellationToken)
    {
        try
        {
            return await RequestWithRetryAsync(AppConfig.ForecastEndpoint, city, true, cancellationToken);
        }
        catch (WeatherException primary) when (primary.Kind is WeatherErrorKind.NotFound or WeatherErrorKind.Forbidden)
        {
            try
            {
                return await RequestWithRetryAsync(AppConfig.InformersEndpoint, city, false, cancellationToken);
            }
            catch (WeatherException) when (primary.Kind == WeatherErrorKind.Forbidden)
            {
                throw primary;
            }
        }
    }

    public static string BuildUrl(string endpoint, City city, bool extended)
    {
        var lat = city.Lat.ToString("F4", CultureInfo.InvariantCulture);
        var lon = city.Lon.ToString("F4", CultureInfo.InvariantCulture);
        var url = $"{endpoint}?lat={lat}&lon={lon}&lang=ru_RU";
        return extended ? url + "&limit=7&hours=true&extra=true" : url;
    }

    private async Task<WeatherResponse> RequestWithRetryAsync(
        string endpoint, City city, bool extended, CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return await RequestOnceAsync(endpoint, city, extended, cancellationToken);
            }
            catch (WeatherException ex) when (IsRetryable(ex.Kind) && attempt < RetryDelays.Length)
            {
                await _delay(RetryDelays[attempt], cancellationToken);
                attempt++;
            }
        }
    }

    private static bool IsRetryable(WeatherErrorKind kind)
    {
        return kind is WeatherErrorKind.Network or WeatherErrorKind.Timeout or WeatherErrorKind.Server;
    }

    private async Task<WeatherResponse> RequestOnceAsync(
        string endpoint, City city, bool extended, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(endpoint, city, extended));
        request.Headers.TryAddWithoutValidation(AppConfig.KeyHeader, _apiKey);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(AppConfig.RequestTimeout);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WeatherException(WeatherErrorKind.Timeout,
                "Сервер не ответил вовремя. Проверьте подключение к интернету и повторите попытку.");
        }
        catch (HttpRequestException ex)
        {
            throw new WeatherException(WeatherErrorKind.Network,
                "Не удалось подключиться к сервису погоды. Проверьте подключение к интернету.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw MapStatus(response.StatusCode);
            }

            string body;
            try
            {
                body = await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                throw new WeatherException(WeatherErrorKind.Network,
                    "Соединение прервано при получении данных.", ex);
            }

            return Parse(body);
        }
    }

    public static WeatherResponse Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new WeatherException(WeatherErrorKind.EmptyResponse, "Сервис вернул пустой ответ.");
        }

        WeatherResponse? parsed;
        try
        {
            var root = JObject.Parse(body);
            NormalizeInformers(root);
            parsed = root.ToObject<WeatherResponse>();
        }
        catch (JsonException ex)
        {
            throw new WeatherException(WeatherErrorKind.EmptyResponse,
                "Не удалось разобрать ответ сервиса погоды.", ex);
        }

        if (parsed?.Fact is null)
        {
            throw new WeatherException(WeatherErrorKind.EmptyResponse,
                "В ответе сервиса нет данных о текущей погоде.");
        }

        return parsed;
    }

    private static void NormalizeInformers(JObject root)
    {
        if (root["forecasts"] is not null || root["forecast"] is not JObject forecast)
        {
            return;
        }

        var day = (JObject)forecast.DeepClone();
        if (day["parts"] is JArray partsArray)
        {
            var parts = new JObject();
            foreach (var part in partsArray.OfType<JObject>())
            {
                var name = part.Value<string>("part_name");
                if (!string.IsNullOrEmpty(name))
                {
                    parts[name] = part;
                }
            }

            day["parts"] = parts;
        }

        root["forecasts"] = new JArray(day);
    }

    public static WeatherException MapStatus(HttpStatusCode status)
    {
        var code = (int)status;
        return code switch
        {
            401 => new WeatherException(WeatherErrorKind.Unauthorized,
                "Ключ API не принят (401). Проверьте ключ в переменной YANDEX_WEATHER_API_KEY или файле .env."),
            403 => new WeatherException(WeatherErrorKind.Forbidden,
                "Доступ запрещён (403). Проверьте тариф и ключ в кабинете Яндекс Погоды для бизнеса: ключ должен быть активен и иметь доступ к запрошенному методу."),
            404 => new WeatherException(WeatherErrorKind.NotFound,
                "Данные для выбранного места не найдены (404)."),
            429 => new WeatherException(WeatherErrorKind.RateLimit,
                "Превышен лимит запросов (429). Подождите и повторите попытку позже."),
            >= 500 => new WeatherException(WeatherErrorKind.Server,
                $"Сервис погоды временно недоступен ({code})."),
            _ => new WeatherException(WeatherErrorKind.Unknown,
                $"Неожиданный ответ сервиса погоды ({code}).")
        };
    }
}
