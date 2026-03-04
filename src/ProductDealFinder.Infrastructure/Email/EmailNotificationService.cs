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

        // Placeholder: retrieve SMTP password from Windows Credential Manager.
        string password = GetSmtpPasswordOrThrow(settings);

        // Placeholder: construct a very simple plain-text message.
        string subject = $"Price alert: {context.Product.Name}";
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
                settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, cancellationToken);

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

        string subject = $"Product added: {context.Product.Name}";
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
                settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, cancellationToken);

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

