using System.Net.Http;
using System.Windows;
using WorkcellOperatorConsole.Core.Services;
using WorkcellOperatorConsole.Core.ViewModels;

namespace WorkcellOperatorConsole;

public partial class App : Application
{
    private HttpClient? _httpClient;
    private OperatorConsoleViewModel? _viewModel;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var configuredUrl = Environment.GetEnvironmentVariable("WORKCELL_API_URL");
        var baseAddress = new Uri(
            string.IsNullOrWhiteSpace(configuredUrl)
                ? "http://localhost:5295/"
                : configuredUrl.EndsWith('/') ? configuredUrl : configuredUrl + "/"
        );

        _httpClient = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(10)
        };
        _viewModel = new OperatorConsoleViewModel(
            new HttpWorkcellApiClient(_httpClient),
            SynchronizationContext.Current
        );

        var window = new MainWindow
        {
            DataContext = _viewModel
        };
        MainWindow = window;
        window.Show();
        _viewModel.StartPolling();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _viewModel?.Dispose();
        _httpClient?.Dispose();
    }
}
