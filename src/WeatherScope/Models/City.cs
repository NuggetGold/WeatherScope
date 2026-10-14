namespace WeatherScope.Models;

public sealed record City(string Name, double Lat, double Lon);

public enum WeatherErrorKind
{
    None,
    Network,
    Timeout,
    Unauthorized,
    Forbidden,
    NotFound,
    RateLimit,
    Server,
    EmptyResponse,
    Unknown
}

public sealed class WeatherException : Exception
{
    public WeatherErrorKind Kind { get; }

    public WeatherException(WeatherErrorKind kind, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
    }
}
