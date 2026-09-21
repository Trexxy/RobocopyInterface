using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using RobocopyInterface.Services;
using RobocopyInterface.ViewModels;

namespace RobocopyInterface;

public partial class App : Application
{
    private readonly IHost _host;
    private Window? _window;

    public App()
    {
        InitializeComponent();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<WindowProvider>();
                services.AddTransient<RobocopyRunner>();
                services.AddTransient<IFilePickerService, FilePickerService>();
                services.AddTransient<MainViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await _host.StartAsync();

        _window = _host.Services.GetRequiredService<MainWindow>();
        _host.Services.GetRequiredService<WindowProvider>().Window = _window;
        _window.Closed += async (_, _) =>
        {
            await _host.StopAsync();
            _host.Dispose();
        };
        _window.Activate();
    }
}
