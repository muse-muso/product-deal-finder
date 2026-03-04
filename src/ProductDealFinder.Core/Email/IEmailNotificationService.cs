using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Core.Email;

public sealed record PriceAlertContext(
    Product Product,
    ProductTarget ProductTarget,
    PriceThreshold Threshold,
    ScrapeResult ScrapeResult);

/// <summary>
/// Context for the "new product added to scan list" notification email.
/// </summary>
public sealed record ProductAddedContext(
    Product Product,
    ProductTarget ProductTarget,
    PriceThreshold Threshold);

/// <summary>
/// Payload sent by the Firefox extension to the desktop app relay for automatic email.
/// </summary>
public sealed record ExtensionAlertPayload(
    string ProductName,
    string RetailerName,
    string Url,
    decimal Price,
    decimal Threshold,
    string Currency);

public interface IEmailNotificationService
{
    /// <summary>
    /// Sends a price alert email to the user based on the supplied context.
    /// </summary>
    Task SendPriceAlertAsync(PriceAlertContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an email notification when a new product is added to the scan list.
    /// </summary>
    Task SendProductAddedNotificationAsync(ProductAddedContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a price alert email from extension relay payload (Firefox extension → desktop app).
    /// </summary>
    Task SendExtensionPriceAlertAsync(ExtensionAlertPayload payload, CancellationToken cancellationToken = default);
}

