using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Markup;
using SmartBite.Client.Services;
using SmartBite.Client.ViewModels;

namespace SmartBite.Client;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Numbers and dates are shown in the Ukrainian format (80,5 and 29.10.2026).
        var culture = new CultureInfo("uk-UA");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag))
        );

        // A recipe or an estimate can take up to a minute, so the timeout is longer than the default.
        var apiUrl =
            Environment.GetEnvironmentVariable("SMARTBITE_API") ?? "http://localhost:5181/";
        var http = new HttpClient
        {
            BaseAddress = new Uri(apiUrl),
            Timeout = TimeSpan.FromMinutes(4),
        };

        // A mistake in the client must not close the window without a word.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                args.Exception.Message,
                "Сталася помилка",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            args.Handled = true;
        };

        var main = new MainViewModel(new ApiClient(http), new DialogService());
        MainWindow = new MainWindow { DataContext = main };
        MainWindow.Show();
    }
}
