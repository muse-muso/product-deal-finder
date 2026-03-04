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
}

