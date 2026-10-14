using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WeatherScope.Models;
using WeatherScope.Services;

namespace WeatherScope.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IWeatherClient _client;
    private readonly WeatherCache _cache;
    private readonly SettingsStore _settings;
    private readonly DispatcherTimer? _timer;
    private readonly RelayCommand _refreshCommand;
    private CancellationTokenSource? _loadCts;

    private City _selectedCity;
    private string _searchText;
    private WeatherSnapshot? _snapshot;
    private bool _isLoading;
    private bool _isRefreshing;
    private bool _hasData;
    private string? _errorMessage;
    private WeatherErrorKind _errorKind;
    private string _statusText = "Ожидание данных";
    private bool _isOffline;
    private bool _isSearchOpen;

    public MainViewModel(
        IWeatherClient client,
        WeatherCache cache,
        SettingsStore settings,
        bool enableTimer = true)
    {
        _client = client;
        _cache = cache;
        _settings = settings;
        _selectedCity = CityCatalog.Find(settings.Load().City) ?? CityCatalog.Default;
        _searchText = _selectedCity.Name;
        _refreshCommand = new RelayCommand(_ => _ = RefreshAsync(), _ => !IsRefreshing);
        RefreshCommand = _refreshCommand;
        SelectCityCommand = new RelayCommand(parameter =>
        {
            if (parameter is City city)
            {
                SelectedCity = city;
            }
        });
        Suggestions = new ObservableCollection<City>(CityCatalog.All);

        if (enableTimer)
        {
            _timer = new DispatcherTimer { Interval = AppConfig.RefreshInterval };
            _timer.Tick += (_, _) => _ = RefreshAsync();
        }
    }

    public ObservableCollection<City> Suggestions { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SelectCityCommand { get; }

    public City SelectedCity
    {
        get => _selectedCity;
        set
        {
            if (!SetProperty(ref _selectedCity, value))
            {
                return;
            }

            _searchText = value.Name;
            OnPropertyChanged(nameof(SearchText));
            IsSearchOpen = false;
            _settings.Save(new AppSettings { City = value.Name });
            _hasData = false;
            Snapshot = null;
            _ = RefreshAsync();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            Suggestions.Clear();
            foreach (var city in CityCatalog.Search(value))
            {
                Suggestions.Add(city);
            }

            IsSearchOpen = Suggestions.Count > 0 && !string.Equals(value, _selectedCity.Name, StringComparison.Ordinal);
        }
    }

    public bool IsSearchOpen
    {
        get => _isSearchOpen;
        set => SetProperty(ref _isSearchOpen, value);
    }

    public WeatherSnapshot? Snapshot
    {
        get => _snapshot;
        private set
        {
            if (SetProperty(ref _snapshot, value))
            {
                OnPropertyChanged(nameof(AccentColor));
                OnPropertyChanged(nameof(AccentBrush));
                OnPropertyChanged(nameof(HasSnapshot));
            }
        }
    }

    public bool HasSnapshot => _snapshot is not null;

    public Color AccentColor => _snapshot?.Accent ?? Color.FromRgb(0x4E, 0xCD, 0xC4);

    public Brush AccentBrush
    {
        get
        {
            var brush = new SolidColorBrush(AccentColor);
            brush.Freeze();
            return brush;
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (SetProperty(ref _isRefreshing, value))
            {
                _refreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    public WeatherErrorKind ErrorKind
    {
        get => _errorKind;
        private set => SetProperty(ref _errorKind, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsOffline
    {
        get => _isOffline;
        private set => SetProperty(ref _isOffline, value);
    }

    public async Task InitializeAsync()
    {
        var cached = _cache.Load(_selectedCity);
        if (cached?.Data is not null)
        {
            ApplyData(cached.Data);
            StatusText = $"Данные из кэша от {cached.SavedAt:HH:mm}";
        }

        _timer?.Start();
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        IsRefreshing = true;
        IsLoading = !_hasData;
        var city = _selectedCity;

        try
        {
            var response = await _client.GetWeatherAsync(city, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            _cache.Save(city, response);
            ApplyData(response);
            ErrorMessage = null;
            ErrorKind = WeatherErrorKind.None;
            IsOffline = false;
            StatusText = $"Обновлено в {DateTime.Now:HH:mm}";
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (WeatherException ex)
        {
            if (ReferenceEquals(_loadCts, cts))
            {
                HandleFailure(city, ex.Kind, ex.Message);
            }
        }
        catch (Exception)
        {
            if (ReferenceEquals(_loadCts, cts))
            {
                HandleFailure(city, WeatherErrorKind.Unknown, "Произошла непредвиденная ошибка при загрузке погоды.");
            }
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts))
            {
                IsLoading = false;
                IsRefreshing = false;
                _loadCts = null;
            }

            cts.Dispose();
        }
    }

    public void Stop()
    {
        _timer?.Stop();
        _loadCts?.Cancel();
    }

    private void ApplyData(WeatherResponse response)
    {
        Snapshot = SnapshotBuilder.Build(response);
        _hasData = true;
    }

    private void HandleFailure(City city, WeatherErrorKind kind, string message)
    {
        ErrorKind = kind;
        ErrorMessage = message;

        if (_hasData)
        {
            IsOffline = true;
            StatusText = "Показаны последние полученные данные";
            return;
        }

        var cached = _cache.Load(city);
        if (cached?.Data is not null)
        {
            ApplyData(cached.Data);
            IsOffline = true;
            StatusText = $"Нет связи. Данные от {cached.SavedAt:HH:mm}";
            return;
        }

        StatusText = "Данные недоступны";
    }
}
