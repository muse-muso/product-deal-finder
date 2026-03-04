using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Core.Scraping;

public interface IScraperEngine
{
    /// <summary>
    /// Scrapes the given product target and returns the latest price information.
    /// Implementations are expected to honor the configuration of the associated RetailerSite.
    /// </summary>
    Task<ScrapePriceResult> ScrapeAsync(ProductTarget target, CancellationToken cancellationToken = default);
}

