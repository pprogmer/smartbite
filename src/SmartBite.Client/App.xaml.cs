using System.Net.Http;
using System.Windows;
using SmartBite.Client.Services;
using SmartBite.Client.ViewModels;

namespace SmartBite.Client;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var apiUrl = Environment.GetEnvironmentVariable("SMARTBITE_API") ?? "http://localhost:5181/";
        var api = new ApiClient(new HttpClient { BaseAddress = new Uri(apiUrl) });

        var main = new MainViewModel();
        main.Navigate(new AuthViewModel(api));
        new MainWindow { DataContext = main }.Show();
    }
}
