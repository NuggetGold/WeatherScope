using System.Windows;

namespace WeatherScope;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                "Произошла непредвиденная ошибка: " + args.Exception.Message,
                "WeatherScope",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
