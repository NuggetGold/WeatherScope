# WeatherScope

Настольная цифровая метеостанция для Windows с данными Яндекс Погоды: ветер, осадки, давление, UV, почасовой график и живые атмосферные эффекты.

## Стек

- C# 12, WPF, MVVM
- Таргеты: `net6.0-windows`, `net8.0-windows`, `net10.0-windows`
- Newtonsoft.Json, xUnit
- Графика: собственные `FrameworkElement` с `OnRender(DrawingContext)`

## Структура

```text
WeatherScope/
├── src/WeatherScope/        Models, Services, ViewModels, Converters, Controls, Themes
├── tests/WeatherScope.Tests/ xUnit-тесты
├── scripts/                 публикация
├── .editorconfig
├── .env.example
└── WeatherScope.sln
```

## API-ключ

Порядок поиска ключа:

1. Переменная окружения `YANDEX_WEATHER_API_KEY`
2. Файл `.env` рядом с `WeatherScope.exe` (образец: `.env.example`)
3. Встроенный ключ по умолчанию в `AppConfig.DefaultApiKey`

Встроенный ключ зашит в исходники. Перед публикацией репозитория замените его или удалите значение по умолчанию.

## Запуск

```powershell
dotnet run --project src/WeatherScope/WeatherScope.csproj -f net10.0-windows
```

## Тесты

```powershell
dotnet test tests/WeatherScope.Tests/WeatherScope.Tests.csproj -f net10.0-windows
```

## Публикация

```powershell
scripts\publish.ps1 -Mode all -Framework all
scripts\publish.ps1 -Mode standalone -Framework net8.0
scripts\publish.ps1 -Mode portable -Framework net10.0
```

- `standalone`: single-file, self-contained, .NET не требуется
- `portable`: single-file, framework-dependent, нужен установленный .NET Desktop Runtime
- Результат: папка `dist/<tfm>-<режим>`

.NET 6 больше не поддерживается Microsoft, при сборке будут предупреждения.

## Что внутри

- Динамический акцентный цвет по температуре
- Компас ветра, барометр, термометр, дуга UV, почасовой график на кубических кривых Безье
- Система частиц: дождь, снег, гроза с молниями, облака
- Smart Tip: рекомендации по грозе, UV, осадкам, ветру, морозу и жаре
- Расчёты: точка росы (Магнус), ветрохолод, тренд давления, окно осадков, фаза Луны
- Поиск по 24 городам, автообновление каждые 10 минут
- Кэш последнего ответа в `%LocalAppData%\WeatherScope`, повторы запросов с паузами 1, 2 и 4 с
- Сообщения об ошибках 401, 403, 404, 429, таймаутах и сбоях сети на русском
- Уменьшенное движение: при отключённых анимациях Windows частицы и анимации выключаются

## Правила проекта

- В исходном коде C# нет комментариев
- Интерфейс полностью на русском
- Секреты не коммитятся: `.env` в `.gitignore`
