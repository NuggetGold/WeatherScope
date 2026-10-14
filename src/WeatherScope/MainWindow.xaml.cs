using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WeatherScope.Models;
using WeatherScope.Services;
using WeatherScope.ViewModels;

namespace WeatherScope;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly HttpClient _http;

    public MainWindow()
    {
        InitializeComponent();

        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var client = new YandexWeatherClient(_http, AppConfig.ResolveApiKey());
        _viewModel = new MainViewModel(client, new WeatherCache(), new SettingsStore());
        DataContext = _viewModel;

        Loaded += async (_, _) =>
        {
            ApplyParticleState();
            await _viewModel.InitializeAsync();
        };
        Closed += (_, _) =>
        {
            _viewModel.Stop();
            _http.Dispose();
        };
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.Snapshot))
            {
                ApplyParticleState();
            }
        };
    }

    private void ApplyParticleState()
    {
        var snapshot = _viewModel.Snapshot;
        if (snapshot is null)
        {
            Particles.Mode = ParticleMode.None;
            return;
        }

        Particles.WindDrift = snapshot.WindDrift;
        Particles.IsThunder = snapshot.IsThunder;
        Particles.Intensity = snapshot.ParticleIntensity;
        Particles.ReducedMotion = !SystemParameters.ClientAreaAnimation;
        Particles.Mode = snapshot.ParticleMode;
    }

    private void SearchBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        SearchBox.SelectAll();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var first = _viewModel.Suggestions.FirstOrDefault();
            if (first is not null)
            {
                _viewModel.SelectedCity = first;
                Keyboard.ClearFocus();
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _viewModel.IsSearchOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Down && _viewModel.IsSearchOpen)
        {
            CityList.Focus();
            if (CityList.Items.Count > 0)
            {
                CityList.SelectedIndex = 0;
            }

            e.Handled = true;
        }
    }

    private void CityList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CityList.SelectedItem is City city)
        {
            CityList.SelectedItem = null;
            _viewModel.SelectedCity = city;
        }
    }
}
