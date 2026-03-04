using System.Windows;
using CredentialManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductDealFinder.Core.Data;
using ProductDealFinder.Core.Email;
using ProductDealFinder.Core.Models;
using ProductDealFinder.Core.Scheduling;
using ProductDealFinder.Core.Scraping;
using ProductDealFinder.Infrastructure.Relay;

namespace ProductDealFinder;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IEmailNotificationService _emailNotificationService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IExtensionRelayService _extensionRelay;

    private const string SmtpCredentialKey = "ProductDealFinder_SMTP";

    /// <summary>Minimum scan interval to avoid hammering retailer sites.</summary>
    private const int MinScanIntervalMinutes = 5;

    public MainWindow(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IEmailNotificationService emailNotificationService,
        IServiceScopeFactory scopeFactory,
        IExtensionRelayService extensionRelay)
    {
        _dbContextFactory = dbContextFactory;
        _emailNotificationService = emailNotificationService;
        _scopeFactory = scopeFactory;
        _extensionRelay = extensionRelay;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var retailers = await db.RetailerSites
                .Where(r => r.IsActive)
                .OrderBy(r => r.Name)
                .ToListAsync();

            RetailerComboBox.ItemsSource = retailers;

            var settings = await db.UserSettings
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            if (settings is not null)
            {
                SmtpHostTextBox.Text = settings.SmtpHost;
                SmtpPortTextBox.Text = settings.SmtpPort.ToString();
                UseSslCheckBox.IsChecked = settings.UseSsl;
                FromEmailTextBox.Text = settings.FromEmail;
                FromDisplayNameTextBox.Text = settings.FromDisplayName;
                DefaultMailboxNameTextBox.Text = settings.DefaultMailboxName ?? "";
                DefaultNotificationEmailTextBox.Text = settings.DefaultNotificationEmail ?? "";
                SmtpUserNameTextBox.Text = settings.SmtpUserName;
                ScanIntervalMinutesTextBox.Text = ((int)settings.ScanInterval.TotalMinutes).ToString();
                ExtensionRelayEnabledCheckBox.IsChecked = settings.ExtensionRelayEnabled ?? false;
                ExtensionRelayPortTextBox.Text = (settings.ExtensionRelayPort > 0 ? settings.ExtensionRelayPort.ToString() : null) ?? "8765";
            }

            // Show relay secret from Credential Manager (masked — displayed as dots by PasswordBox).
            var relaySecret = GetRelaySecretFromCredManager();
            if (!string.IsNullOrEmpty(relaySecret))
                ExtensionRelaySecretBox.Password = relaySecret;

            StatusTextBox.Text = "Loaded settings and retailers.";
        }
        catch (Exception ex)
        {
            StatusTextBox.Text = $"Error loading data: {ex.Message}";
        }
    }

    private async void OnSaveEmailSettingsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(SmtpPortTextBox.Text, out int port))
            {
                StatusTextBox.Text = "SMTP Port must be a number.";
                return;
            }

            if (!int.TryParse(ScanIntervalMinutesTextBox.Text, out int minutes) || minutes < MinScanIntervalMinutes)
            {
                StatusTextBox.Text = $"Scan interval must be at least {MinScanIntervalMinutes} minutes.";
                return;
            }

            string smtpHost = SmtpHostTextBox.Text.Trim();
            string fromEmail = FromEmailTextBox.Text.Trim();
            string smtpUser = SmtpUserNameTextBox.Text.Trim();
            string password = SmtpPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(smtpHost) ||
                string.IsNullOrWhiteSpace(fromEmail) ||
                string.IsNullOrWhiteSpace(smtpUser))
            {
                StatusTextBox.Text = "SMTP Host, From Email, and SMTP Username are required.";
                return;
            }

            // Basic email format validation.
            if (!IsValidEmail(fromEmail))
            {
                StatusTextBox.Text = "From Email does not look like a valid email address.";
                return;
            }

            var notificationEmail = DefaultNotificationEmailTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(notificationEmail) && !IsValidEmail(notificationEmail))
            {
                StatusTextBox.Text = "Default Email Address does not look like a valid email address.";
                return;
            }

            // Save SMTP password to Windows Credential Manager when provided; keep existing if blank.
            bool haveExistingCredential = false;
            using (var existing = new Credential { Target = SmtpCredentialKey, Type = CredentialType.Generic })
            {
                haveExistingCredential = existing.Load();
            }

            if (string.IsNullOrEmpty(password))
            {
                if (!haveExistingCredential)
                {
                    StatusTextBox.Text = "Please enter an SMTP password (leave blank only if you already saved one).";
                    return;
                }
            }
            else
            {
                using (var cred = new Credential
                {
                    Target = SmtpCredentialKey,
                    Username = smtpUser,
                    Password = password,
                    PersistanceType = PersistanceType.LocalComputer,
                    Type = CredentialType.Generic
                })
                {
                    cred.Save();
                }
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var settings = await db.UserSettings
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            if (settings is null)
            {
                settings = new UserSettings();
                db.UserSettings.Add(settings);
            }

            settings.SmtpHost = smtpHost;
            settings.SmtpPort = port;
            settings.UseSsl = UseSslCheckBox.IsChecked == true;
            settings.FromEmail = fromEmail;
            settings.FromDisplayName = string.IsNullOrWhiteSpace(FromDisplayNameTextBox.Text)
                ? null
                : FromDisplayNameTextBox.Text.Trim();
            settings.DefaultMailboxName = string.IsNullOrWhiteSpace(DefaultMailboxNameTextBox.Text)
                ? null
                : DefaultMailboxNameTextBox.Text.Trim();
            settings.DefaultNotificationEmail = string.IsNullOrWhiteSpace(notificationEmail)
                ? null
                : notificationEmail;
            settings.SmtpUserName = smtpUser;
            settings.SmtpPasswordCredentialKey = SmtpCredentialKey;
            settings.ScanInterval = TimeSpan.FromMinutes(minutes);

            settings.ExtensionRelayEnabled = ExtensionRelayEnabledCheckBox.IsChecked == true;
            if (int.TryParse(ExtensionRelayPortTextBox.Text.Trim(), out int relayPort) && relayPort > 0 && relayPort < 65536)
                settings.ExtensionRelayPort = relayPort;
            else
                settings.ExtensionRelayPort = 8765;

            // Relay secret is managed in Windows Credential Manager by ExtensionRelayService.
            // Auto-generate it here so it is ready as soon as the relay is saved & started.
            if (settings.ExtensionRelayEnabled == true)
                EnsureRelaySecretInCredManager(settings);

            // Clear the legacy plain-text DB column now that the secret lives in Credential Manager.
            settings.ExtensionRelaySecret = null;

            await db.SaveChangesAsync();

            // Refresh the masked display.
            var newRelaySecret = GetRelaySecretFromCredManager();
            if (!string.IsNullOrEmpty(newRelaySecret))
                ExtensionRelaySecretBox.Password = newRelaySecret;

            try { await _extensionRelay.RestartAsync(); } catch (Exception ex) { StatusTextBox.Text = "Settings saved. Relay restart failed: " + ex.Message; return; }

            StatusTextBox.Text = "Email and scan settings saved successfully.";
        }
        catch (Exception ex)
        {
            StatusTextBox.Text = $"Error saving email settings: {ex.Message}";
        }
    }

    private async void OnSaveProductTargetClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string productName = ProductNameTextBox.Text.Trim();
            string modelNumber = ModelNumberTextBox.Text.Trim();
            string url = ProductPageUrlTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(productName) || string.IsNullOrWhiteSpace(url))
            {
                StatusTextBox.Text = "Product Name and Product Page URL are required.";
                return;
            }

            // Validate the URL before saving to prevent SSRF via crafted DB entries.
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUri) ||
                !parsedUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                StatusTextBox.Text = "Product Page URL must start with https://";
                return;
            }

            if (!decimal.TryParse(ThresholdValueTextBox.Text, out decimal thresholdValue))
            {
                StatusTextBox.Text = "Price Threshold must be a number.";
                return;
            }

            if (RetailerComboBox.SelectedItem is not RetailerSite retailer)
            {
                StatusTextBox.Text = "Please select a retailer.";
                return;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var product = new Product
            {
                Name = productName,
                ModelNumber = string.IsNullOrWhiteSpace(modelNumber) ? null : modelNumber.Trim()
            };

            db.Products.Add(product);
            await db.SaveChangesAsync();

            var target = new ProductTarget
            {
                ProductId = product.Id,
                RetailerSiteId = retailer.Id,
                ProductPageUrl = url,
                PriceSelectorOverride = string.IsNullOrWhiteSpace(PriceSelectorOverrideTextBox.Text)
                    ? null
                    : PriceSelectorOverrideTextBox.Text.Trim(),
                IsActive = true
            };

            db.ProductTargets.Add(target);
            await db.SaveChangesAsync();

            var threshold = new PriceThreshold
            {
                ProductTargetId = target.Id,
                Value = thresholdValue,
                Currency = "AUD",
                Comparison = ThresholdComparison.LessThanOrEqual,
                IsActive = true
            };

            db.PriceThresholds.Add(threshold);
            await db.SaveChangesAsync();

            target.Product = product;
            target.RetailerSite = retailer;
            var productAddedContext = new ProductAddedContext(product, target, threshold);
            try
            {
                await _emailNotificationService.SendProductAddedNotificationAsync(productAddedContext);
                StatusTextBox.Text = $"Product, target, and threshold saved. Target ID: {target.Id}. Confirmation email sent.";
            }
            catch (Exception emailEx)
            {
                string hint = "";
                if (emailEx.Message.Contains("SmtpClientAuthentication is disabled", StringComparison.OrdinalIgnoreCase))
                    hint = " (Outlook/Microsoft 365 may have SMTP auth disabled — see README Troubleshooting.)";
                else if (emailEx.Message.Contains("Username and Password not accepted", StringComparison.OrdinalIgnoreCase) || emailEx.Message.Contains("BadCredentials", StringComparison.OrdinalIgnoreCase))
                    hint = " (For Gmail use an App password, not your normal password — see README Troubleshooting.)";
                StatusTextBox.Text = $"Product, target, and threshold saved. Target ID: {target.Id}. Email notification failed: {emailEx.Message}{hint}";
            }
        }
        catch (Exception ex)
        {
            StatusTextBox.Text = $"Error saving product/target: {ex.Message}";
        }
    }

    private async void OnRunScanNowClick(object sender, RoutedEventArgs e)
    {
        RunScanNowButton.IsEnabled = false;
        StatusTextBox.Text = "Manual scan started…";
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var scanService = scope.ServiceProvider.GetRequiredService<IScanService>();
            await scanService.RunScanOnceAsync();
            StatusTextBox.Text = "Manual scan completed. Check Management Window for latest prices and history.";
        }
        catch (Exception ex)
        {
            StatusTextBox.Text = $"Manual scan failed: {ex.Message}";
        }
        finally
        {
            RunScanNowButton.IsEnabled = true;
        }
    }

    private void OnOpenManagementWindowClick(object sender, RoutedEventArgs e)
    {
        var window = new ManageDataWindow(_dbContextFactory)
        {
            Owner = this
        };
        window.Show();
    }

    // -------------------------------------------------------------------------
    // Relay secret helpers
    // -------------------------------------------------------------------------

    private static string? GetRelaySecretFromCredManager()
    {
        using var cred = new Credential { Target = ExtensionRelayService.RelaySecretCredentialKey, Type = CredentialType.Generic };
        return cred.Load() ? cred.Password : null;
    }

    /// <summary>
    /// Ensures a relay secret exists in Credential Manager. Migrates any existing plain-text
    /// secret from the DB settings row (preserves pairing for existing installs).
    /// </summary>
    private static void EnsureRelaySecretInCredManager(UserSettings? settings)
    {
        using var existing = new Credential { Target = ExtensionRelayService.RelaySecretCredentialKey, Type = CredentialType.Generic };
        if (existing.Load()) return;

        var secret = !string.IsNullOrWhiteSpace(settings?.ExtensionRelaySecret)
            ? settings.ExtensionRelaySecret
            : Guid.NewGuid().ToString("N");

        using var newCred = new Credential
        {
            Target = ExtensionRelayService.RelaySecretCredentialKey,
            Username = "relay",
            Password = secret,
            PersistanceType = PersistanceType.LocalComputer,
            Type = CredentialType.Generic
        };
        newCred.Save();
    }

    // -------------------------------------------------------------------------
    // Input validation helpers
    // -------------------------------------------------------------------------

    private static bool IsValidEmail(string email)
        => !string.IsNullOrWhiteSpace(email) &&
           System.Text.RegularExpressions.Regex.IsMatch(
               email,
               @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
               System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
