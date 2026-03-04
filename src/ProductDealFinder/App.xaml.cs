using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ProductDealFinder.Core.Data;
using ProductDealFinder.Core.Email;
using ProductDealFinder.Core.Scheduling;
using ProductDealFinder.Core.Scraping;
using ProductDealFinder.Infrastructure.Email;
using ProductDealFinder.Infrastructure.Relay;
using ProductDealFinder.Infrastructure.Scheduling;
using ProductDealFinder.Infrastructure.Scraping;
using MaterialDesignThemes.Wpf;

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

        ApplyWindowsSystemTheme();

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
            .ConfigureLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information))
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

                // Extension relay (Firefox extension → desktop app → email)
                services.AddSingleton<IExtensionRelayService, ExtensionRelayService>();

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

        var relay = _host.Services.GetRequiredService<IExtensionRelayService>();
        relay.StartAsync().GetAwaiter().GetResult();

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
            try
            {
                var relay = _host.Services.GetService<IExtensionRelayService>();
                relay?.StopAsync().GetAwaiter().GetResult();
            }
            catch { }
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    /// <summary>
    /// Applies light or dark theme to match Windows desktop setting (Settings → Personalization → Colors → Choose your mode).
    /// </summary>
    private static void ApplyWindowsSystemTheme()
    {
        try
        {
            bool useLightTheme = true;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int value)
                    useLightTheme = value != 0;
            }
            catch
            {
                // Default to light if registry read fails (e.g. older Windows)
            }

            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(useLightTheme ? BaseTheme.Light : BaseTheme.Dark);
            paletteHelper.SetTheme(theme);
        }
        catch
        {
            // Keep default theme from App.xaml if Material Design theme switch fails
        }
    }
}
