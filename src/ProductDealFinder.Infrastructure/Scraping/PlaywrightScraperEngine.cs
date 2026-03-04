using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ProductDealFinder.Core.Models;
using ProductDealFinder.Core.Scraping;

namespace ProductDealFinder.Infrastructure.Scraping;

/// <summary>
/// Playwright-based implementation of IScraperEngine that delegates to retailer-specific adapters.
/// </summary>
public class PlaywrightScraperEngine : IScraperEngine
{
    private readonly IEnumerable<IProductPageAdapter> _adapters;
    private readonly ILogger<PlaywrightScraperEngine> _logger;

    public PlaywrightScraperEngine(
        IEnumerable<IProductPageAdapter> adapters,
        ILogger<PlaywrightScraperEngine> logger)
    {
        _adapters = adapters;
        _logger = logger;
    }

    public async Task<ScrapePriceResult> ScrapeAsync(ProductTarget target, CancellationToken cancellationToken = default)
    {
        if (target.RetailerSite is null)
        {
            throw new InvalidOperationException("ProductTarget.RetailerSite must be loaded before scraping.");
        }

        string retailerCode = target.RetailerSite.Code;

        IProductPageAdapter? adapter = _adapters
            .FirstOrDefault(a => string.Equals(a.RetailerCode, retailerCode, StringComparison.OrdinalIgnoreCase))
            ?? _adapters.FirstOrDefault(a => string.Equals(a.RetailerCode, RetailerCodes.Generic, StringComparison.OrdinalIgnoreCase));

        if (adapter is null)
        {
            throw new InvalidOperationException($"No scraper adapter registered for retailer code '{retailerCode}' and no generic adapter is available.");
        }

        _logger.LogInformation("Scraping {Retailer} for product target {TargetId}", retailerCode, target.Id);

        using var playwrightCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        playwrightCts.CancelAfter(TimeSpan.FromSeconds(60));

        using var _ = playwrightCts; // ensure disposal

        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(target.ProductPageUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30_000
        });

        return await adapter.ScrapeAsync(page, target, playwrightCts.Token);
    }
}

