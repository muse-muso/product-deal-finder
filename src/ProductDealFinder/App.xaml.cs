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
