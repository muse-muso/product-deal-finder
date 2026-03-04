using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProductDealFinder.Core.Data;
using ProductDealFinder.Core.Email;
using ProductDealFinder.Core.Scheduling;
using ProductDealFinder.Core.Scraping;
using ProductDealFinder.Infrastructure.Email;
using ProductDealFinder.Infrastructure.Scheduling;
using ProductDealFinder.Infrastructure.Scraping;

namespace ProductDealFinder;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Catch unhandled exceptions so the app doesn't exit silently
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            string message = args.Exception?.ToString() ?? "Unknown error";
            System.Diagnostics.Debug.WriteLine(message);
            MessageBox.Show(
                "An error occurred:\n\n" + (args.Exception?.Message ?? "Unknown") + "\n\nSee Details for full message.",
                "Product Deal Finder - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            // Optionally rethrow to exit: args.Handled = false;
        };

        try
        {
            _host = Host.CreateDefaultBuilder(e.Args)
            .ConfigureServices((_, services) =>
            {
                string dataDirectory = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ProductDealFinder");

                System.IO.Directory.CreateDirectory(dataDirectory);

                string dbPath = System.IO.Path.Combine(dataDirectory, "product-deal-finder.db");
                string connectionString = $"Data Source={dbPath}";

                services.AddDbContextFactory<AppDbContext>(options =>
                {
                    options.UseSqlite(connectionString);
                });

                // Core services
                services.AddScoped<IScraperEngine, PlaywrightScraperEngine>();
                services.AddScoped<IEmailNotificationService, EmailNotificationService>();
                services.AddScoped<IScanService, ScanService>();

                // Adapters
                services.AddSingleton<IProductPageAdapter, JbHiFiAdapter>();
                services.AddSingleton<IProductPageAdapter, AmazonAuAdapter>();
                services.AddSingleton<IProductPageAdapter, TheGoodGuysAdapter>();
                services.AddSingleton<IProductPageAdapter, OfficeworksAdapter>();
                services.AddSingleton<IProductPageAdapter, HarveyNormanAdapter>();
                services.AddSingleton<IProductPageAdapter, GenericCssSelectorAdapter>();

                // Background scanning
                services.AddHostedService<BackgroundScanHostedService>();

                // WPF
                services.AddSingleton<MainWindow>();
                services.AddTransient<ManageDataWindow>();
            })
            .Build();

        using (IServiceScope scope = _host.Services.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var db = dbFactory.CreateDbContext();
            AppDbSeeder.SeedAsync(db).GetAwaiter().GetResult();
        }

        _host.Start();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Failed to start the application:\n\n" + ex.Message + "\n\n" + ex.StackTrace,
                "Product Deal Finder - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
