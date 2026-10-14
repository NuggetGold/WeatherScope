using System.Net;
using WeatherScope.Models;
using WeatherScope.Services;
using Xunit;

namespace WeatherScope.Tests;

public class ClientTests
{
    private const string SampleJson = """
    {
      "now": 1700000000,
      "info": { "lat": 55.75, "lon": 37.61, "tzinfo": { "offset": 10800, "name": "Europe/Moscow" } },
      "fact": {
        "temp": -3, "feels_like": -8, "condition": "light-snow", "wind_speed": 4.5, "wind_gust": 9,
        "wind_dir": "nw", "pressure_mm": 749, "humidity": 85, "uv_index": 1, "prec_type": 3,
        "prec_strength": 0.5, "is_thunder": false
      },
      "forecasts": [
        {
          "date": "2023-11-14", "date_ts": 1699920000, "sunrise": "07:30", "sunset": "16:30", "moon_code": 4,
          "parts": { "day_short": { "temp": -2, "condition": "snow", "wind_speed": 5, "prec_mm": 1.5 },
                     "night_short": { "temp": -6 } },
          "hours": [
            { "hour": "0", "hour_ts": 1700000000, "temp": -3, "pressure_mm": 749, "prec_mm": 0.2 },
            { "hour": "1", "hour_ts": 1700003600, "temp": -4, "pressure_mm": 748, "prec_mm": 0 },
            { "hour": "2", "hour_ts": 1700007200, "temp": -4, "pressure_mm": 747, "prec_mm": 0 }
          ]
        }
      ]
    }
    """;

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();
        public List<HttpRequestMessage> Requests { get; } = new();

        public StubHandler Enqueue(HttpStatusCode status, string body = "")
        {
            _responses.Enqueue(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });
            return this;
        }

        public StubHandler EnqueueThrow(Exception exception)
        {
            _responses.Enqueue(_ => throw exception);
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue()(request));
        }
    }

    private static YandexWeatherClient Create(StubHandler handler)
    {
        return new YandexWeatherClient(new HttpClient(handler), "test-key", (_, _) => Task.CompletedTask);
    }

    [Fact]
    public async Task Success_ParsesAndSendsHeader()
    {
        var handler = new StubHandler().Enqueue(HttpStatusCode.OK, SampleJson);
        var result = await Create(handler).GetWeatherAsync(CityCatalog.Default, CancellationToken.None);

        Assert.Equal(-3, result.Fact!.Temp);
        Assert.Equal("nw", result.Fact.WindDir);
        Assert.Single(result.Forecasts);
        Assert.Equal("test-key", handler.Requests[0].Headers.GetValues(AppConfig.KeyHeader).Single());
        Assert.Contains("lang=ru_RU", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("limit=7", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("hours=true", handler.Requests[0].RequestUri!.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, WeatherErrorKind.Unauthorized)]
    [InlineData((HttpStatusCode)429, WeatherErrorKind.RateLimit)]
    public async Task ClientErrors_AreMappedWithoutRetry(HttpStatusCode status, WeatherErrorKind kind)
    {
        var handler = new StubHandler().Enqueue(status);
        var ex = await Assert.ThrowsAsync<WeatherException>(
            () => Create(handler).GetWeatherAsync(CityCatalog.Default, CancellationToken.None));

        Assert.Equal(kind, ex.Kind);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Forbidden_HasHumanMessageAboutTariff()
    {
        var handler = new StubHandler()
            .Enqueue(HttpStatusCode.Forbidden)
            .Enqueue(HttpStatusCode.Forbidden);
        var ex = await Assert.ThrowsAsync<WeatherException>(
            () => Create(handler).GetWeatherAsync(CityCatalog.Default, CancellationToken.None));

        Assert.Equal(WeatherErrorKind.Forbidden, ex.Kind);
        Assert.Contains("тариф", ex.Message);
        Assert.Contains("ключ", ex.Message);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Forbidden_FallsBackToInformersAndNormalizesSchema()
    {
        const string informersJson = """
        {
          "now": 1700000000,
          "fact": { "temp": 4, "feels_like": 1, "condition": "cloudy", "wind_speed": 3, "wind_dir": "e", "pressure_mm": 750, "humidity": 70 },
          "forecast": {
            "date": "2023-11-15", "sunrise": "07:31", "sunset": "16:29", "moon_code": 8,
            "parts": [
              { "part_name": "day_short", "temp": 5, "condition": "cloudy" },
              { "part_name": "night_short", "temp": 1 }
            ]
          }
        }
        """;
        var handler = new StubHandler()
            .Enqueue(HttpStatusCode.Forbidden)
            .Enqueue(HttpStatusCode.OK, informersJson);
        var result = await Create(handler).GetWeatherAsync(CityCatalog.Default, CancellationToken.None);

        Assert.Equal(4, result.Fact!.Temp);
        Assert.Single(result.Forecasts);
        Assert.Equal(5, result.Forecasts[0].Parts!.DayShort!.Temp);
        Assert.Equal(1, result.Forecasts[0].Parts!.NightShort!.Temp);
        Assert.Equal("Среда", SnapshotBuilder.DayLabel(result.Forecasts[0], 10800));
        Assert.StartsWith(AppConfig.InformersEndpoint, handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public void DayLabel_UsesLocalDateEvenWhenTimestampIsEarlierInUtc()
    {
        var day = new ForecastDay { Date = "2023-11-14", DateTs = 1699909200 };
        Assert.Equal("Вторник", SnapshotBuilder.DayLabel(day, 10800));
        var onlyTimestamp = new ForecastDay { DateTs = 1699909200 };
        Assert.Equal("Вторник", SnapshotBuilder.DayLabel(onlyTimestamp, 10800));
    }

    [Fact]
    public async Task NotFound_FallsBackToInformers()
    {
        var handler = new StubHandler()
            .Enqueue(HttpStatusCode.NotFound)
            .Enqueue(HttpStatusCode.OK, SampleJson);
        var result = await Create(handler).GetWeatherAsync(CityCatalog.Default, CancellationToken.None);

        Assert.NotNull(result.Fact);
        Assert.Equal(2, handler.Requests.Count);
        Assert.StartsWith(AppConfig.InformersEndpoint, handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task ServerError_IsRetriedThenSucceeds()
    {
        var handler = new StubHandler()
            .Enqueue(HttpStatusCode.InternalServerError)
            .Enqueue(HttpStatusCode.BadGateway)
            .Enqueue(HttpStatusCode.OK, SampleJson);
        var result = await Create(handler).GetWeatherAsync(CityCatalog.Default, CancellationToken.None);

        Assert.NotNull(result.Fact);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task NetworkFailure_ExhaustsRetries()
    {
        var handler = new StubHandler();
        for (var i = 0; i < 4; i++)
        {
            handler.EnqueueThrow(new HttpRequestException("offline"));
        }

        var ex = await Assert.ThrowsAsync<WeatherException>(
            () => Create(handler).GetWeatherAsync(CityCatalog.Default, CancellationToken.None));

        Assert.Equal(WeatherErrorKind.Network, ex.Kind);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public void Parse_RejectsEmptyAndMissingFact()
    {
        Assert.Equal(WeatherErrorKind.EmptyResponse,
            Assert.Throws<WeatherException>(() => YandexWeatherClient.Parse("")).Kind);
        Assert.Equal(WeatherErrorKind.EmptyResponse,
            Assert.Throws<WeatherException>(() => YandexWeatherClient.Parse("{}")).Kind);
        Assert.Equal(WeatherErrorKind.EmptyResponse,
            Assert.Throws<WeatherException>(() => YandexWeatherClient.Parse("not json")).Kind);
    }

    [Fact]
    public void Parse_ToleratesMissingOptionalFields()
    {
        var parsed = YandexWeatherClient.Parse("""{ "fact": { "temp": 5 } }""");
        Assert.Equal(5, parsed.Fact!.Temp);
        Assert.Null(parsed.Fact.TempWater);
        Assert.Null(parsed.Fact.UvIndex);
        Assert.Empty(parsed.Forecasts);
    }

    [Fact]
    public void SnapshotBuilder_BuildsRussianSnapshot()
    {
        var snapshot = SnapshotBuilder.Build(YandexWeatherClient.Parse(SampleJson));

        Assert.Equal("−3°", snapshot.TempText);
        Assert.Equal("Небольшой снег", snapshot.ConditionText);
        Assert.Equal(135, snapshot.WindArrowDegrees);
        Assert.Equal(ParticleMode.Snow, snapshot.ParticleMode);
        Assert.Equal(3, snapshot.Hourly.Count);
        Assert.Equal("01:00", snapshot.Hourly[0].Label);
        Assert.Equal("давление падает", snapshot.PressureTrendText);
        Assert.Equal("Низкий", snapshot.UvLabel);
        Assert.Contains(snapshot.Details, d => d.Label == "Фаза Луны" && d.Value == "Последняя четверть");
        Assert.Equal("Вторник", snapshot.Daily[0].DayLabel);
        Assert.Contains("порывы до 9 м/с", snapshot.WindSummary);
    }

    [Fact]
    public void BuildUrl_UsesInvariantCulture()
    {
        var url = YandexWeatherClient.BuildUrl(AppConfig.ForecastEndpoint, new City("Тест", 55.5, 37.25), true);
        Assert.Contains("lat=55.5000", url);
        Assert.Contains("lon=37.2500", url);
    }
}
