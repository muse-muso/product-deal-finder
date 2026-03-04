using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Core.Email;

public sealed record PriceAlertContext(
    Product Product,
    ProductTarget ProductTarget,
    PriceThreshold Threshold,
    ScrapeResult ScrapeResult);

public interface IEmailNotificationService
{
    /// <summary>
    /// Sends a price alert email to the user based on the supplied context.
    /// </summary>
    Task SendPriceAlertAsync(PriceAlertContext context, CancellationToken cancellationToken = default);
}

