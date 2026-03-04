using Microsoft.Playwright;
using ProductDealFinder.Core.Models;
using ProductDealFinder.Core.Scraping;

namespace ProductDealFinder.Infrastructure.Scraping;

public interface IProductPageAdapter
{
    string RetailerCode { get; }

    Task<ScrapePriceResult> ScrapeAsync(IPage page, ProductTarget target, CancellationToken cancellationToken);
}

