using System.Text;
using CredentialManagement;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductDealFinder.Core.Data;
using ProductDealFinder.Core.Email;
using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Infrastructure.Email;

/// <summary>
/// SMTP-based implementation of IEmailNotificationService using MailKit and Windows Credential Manager.
/// This is a skeleton; the message formatting and configuration handling will be expanded later.
/// </summary>
public class EmailNotificationService : IEmailNotificationService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<EmailNotificationService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Strips characters that could be used for MIME header injection (\r, \n, \0).
    /// Call on any user-supplied or scraped string before using it in an email subject or address field.
    /// </summary>
    private static string SanitizeHeaderValue(string? value)
        => (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("\0", string.Empty);

    public async Task SendPriceAlertAsync(PriceAlertContext context, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        UserSettings? settings = await db.UserSettings
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            throw new InvalidOperationException("User email settings are not configured.");
        }

        string password = GetSmtpPasswordOrThrow(settings);

        string subject = $"Price alert: {SanitizeHeaderValue(context.Product.Name)}";
        var bodyBuilder = new StringBuilder();
        bodyBuilder.AppendLine($"Product: {context.Product.Name}");
        bodyBuilder.AppendLine($"Model: {context.Product.ModelNumber}");
        bodyBuilder.AppendLine($"Retailer: {context.ProductTarget.RetailerSite.Name}");
        bodyBuilder.AppendLine($"Threshold: {context.Threshold.Comparison} {context.Threshold.Value} {context.Threshold.Currency}");
        bodyBuilder.AppendLine($"Current price: {context.ScrapeResult.Price} {context.ScrapeResult.Currency}");
        bodyBuilder.AppendLine($"Product page: {context.ProductTarget.ProductPageUrl}");

        string toAddress = string.IsNullOrWhiteSpace(settings.DefaultNotificationEmail) ? settings.FromEmail : settings.DefaultNotificationEmail.Trim();

        using var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress(
            settings.FromDisplayName ?? settings.FromEmail,
            settings.FromEmail));
        message.To.Add(new MimeKit.MailboxAddress(toAddress, toAddress));
        message.Subject = subject;
        message.Body = new MimeKit.TextPart("plain")
        {
            Text = bodyBuilder.ToString()
        };

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort,
                // Always require TLS; SecureSocketOptions.StartTls mandates TLS negotiation.
                // Using Auto would silently allow a plaintext fallback if the server doesn't
                // offer STARTTLS, which could expose credentials.
                SecureSocketOptions.StartTls, cancellationToken);

            await client.AuthenticateAsync(settings.SmtpUserName, password, cancellationToken);

            await client.SendAsync(message, cancellationToken);
        }
        finally
        {
            await client.DisconnectAsync(true, cancellationToken);
        }
    }

    public async Task SendProductAddedNotificationAsync(ProductAddedContext context, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        UserSettings? settings = await db.UserSettings
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            _logger.LogWarning("Cannot send product-added email: user email settings are not configured.");
            return;
        }

        string password;
        try
        {
            password = GetSmtpPasswordOrThrow(settings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot send product-added email: SMTP credential not available.");
            return;
        }

        string subject = $"Product added: {SanitizeHeaderValue(context.Product.Name)}";
        var bodyBuilder = new StringBuilder();
        bodyBuilder.AppendLine("A new product has been added to your scan list.");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine($"Product: {context.Product.Name}");
        if (!string.IsNullOrWhiteSpace(context.Product.ModelNumber))
            bodyBuilder.AppendLine($"Model: {context.Product.ModelNumber}");
        bodyBuilder.AppendLine($"Retailer: {context.ProductTarget.RetailerSite.Name}");
        bodyBuilder.AppendLine($"Alert when price: {context.Threshold.Comparison} {context.Threshold.Value} {context.Threshold.Currency}");
        bodyBuilder.AppendLine($"Product page: {context.ProductTarget.ProductPageUrl}");

        string toAddress = string.IsNullOrWhiteSpace(settings.DefaultNotificationEmail) ? settings.FromEmail : settings.DefaultNotificationEmail.Trim();

        using var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress(
            settings.FromDisplayName ?? settings.FromEmail,
            settings.FromEmail));
        message.To.Add(new MimeKit.MailboxAddress(toAddress, toAddress));
        message.Subject = subject;
        message.Body = new MimeKit.TextPart("plain")
        {
            Text = bodyBuilder.ToString()
        };

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort,
                SecureSocketOptions.StartTls, cancellationToken);

            await client.AuthenticateAsync(settings.SmtpUserName, password, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            _logger.LogInformation("Product-added notification sent for product {ProductName}", context.Product.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send product-added notification for product {ProductName}", context.Product.Name);
            throw;
        }
        finally
        {
            await client.DisconnectAsync(true, cancellationToken);
        }
    }

    public async Task SendExtensionPriceAlertAsync(ExtensionAlertPayload payload, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        UserSettings? settings = await db.UserSettings
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            _logger.LogWarning("Cannot send extension price alert: user email settings are not configured.");
            throw new InvalidOperationException("User email settings are not configured.");
        }

        string password = GetSmtpPasswordOrThrow(settings);
        string toAddress = string.IsNullOrWhiteSpace(settings.DefaultNotificationEmail) ? settings.FromEmail : settings.DefaultNotificationEmail.Trim();

        string subject = "Price alert: " + SanitizeHeaderValue(payload.ProductName);
        var body = new StringBuilder();
        body.AppendLine("Product: " + payload.ProductName);
        body.AppendLine("Retailer: " + payload.RetailerName);
        body.AppendLine("Current price: " + payload.Currency + " " + payload.Price.ToString("F2"));
        body.AppendLine("Your target: " + payload.Currency + " " + payload.Threshold.ToString("F2"));
        body.AppendLine("Product page: " + payload.Url);

        using var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress(
            settings.FromDisplayName ?? settings.FromEmail,
            settings.FromEmail));
        message.To.Add(new MimeKit.MailboxAddress(toAddress, toAddress));
        message.Subject = subject;
        message.Body = new MimeKit.TextPart("plain") { Text = body.ToString() };

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort,
                SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(settings.SmtpUserName, password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            _logger.LogInformation("Extension price alert sent for {ProductName}", payload.ProductName);
        }
        finally
        {
            await client.DisconnectAsync(true, cancellationToken);
        }
    }

    private static string GetSmtpPasswordOrThrow(UserSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SmtpPasswordCredentialKey))
        {
            throw new InvalidOperationException("SMTP password credential key is not set in user settings.");
        }

        using var cred = new Credential { Target = settings.SmtpPasswordCredentialKey, Type = CredentialType.Generic };
        if (!cred.Load())
        {
            throw new InvalidOperationException($"SMTP password could not be loaded from Windows Credential Manager using key '{settings.SmtpPasswordCredentialKey}'.");
        }

        return cred.Password;
    }
}

