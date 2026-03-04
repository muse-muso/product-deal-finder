using System.Windows;
using CredentialManagement;
using Microsoft.EntityFrameworkCore;
using ProductDealFinder.Core.Data;
using ProductDealFinder.Core.Email;
using ProductDealFinder.Core.Models;

namespace ProductDealFinder;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IEmailNotificationService _emailNotificationService;

    private const string SmtpCredentialKey = "ProductDealFinder_SMTP";

    public MainWindow(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IEmailNotificationService emailNotificationService)
    {
        _dbContextFactory = dbContextFactory;
        _emailNotificationService = emailNotificationService;
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
            }

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

            if (!int.TryParse(ScanIntervalMinutesTextBox.Text, out int minutes) || minutes <= 0)
            {
                StatusTextBox.Text = "Scan interval must be a positive number of minutes.";
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

            if (string.IsNullOrEmpty(password))
            {
                StatusTextBox.Text = "Please enter an SMTP password.";
                return;
            }

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
            settings.DefaultNotificationEmail = string.IsNullOrWhiteSpace(DefaultNotificationEmailTextBox.Text)
                ? null
                : DefaultNotificationEmailTextBox.Text.Trim();
            settings.SmtpUserName = smtpUser;
            settings.SmtpPasswordCredentialKey = SmtpCredentialKey;
            settings.ScanInterval = TimeSpan.FromMinutes(minutes);

            await db.SaveChangesAsync();

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

            // Send "product added" notification email (best-effort; don't fail the save)
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

    private void OnOpenManagementWindowClick(object sender, RoutedEventArgs e)
    {
        var window = new ManageDataWindow(_dbContextFactory)
        {
            Owner = this
        };
        window.Show();
    }
}