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

    /// <summary>
    /// Allowed HTTPS hostnames for product pages. Prevents SSRF / local-file access via
    /// crafted URLs in the database (e.g. file:///, http://internal-host/).
    /// </summary>
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "www.jbhifi.com.au",
        "jbhifi.com.au",
        "www.amazon.com.au",
        "amazon.com.au",
        "www.thegoodguys.com.au",
        "thegoodguys.com.au",
        "www.officeworks.com.au",
        "officeworks.com.au",
        "www.harveynorman.com.au",
        "harveynorman.com.au"
    };

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

        // Validate the URL before launching the browser to prevent SSRF and local file access.
        ValidateProductUrl(target.ProductPageUrl);

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

    /// <summary>
    /// Throws if the URL is not an https:// address on a supported retailer domain.
    /// </summary>
    private static void ValidateProductUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Product URL is not a valid absolute URI: '{url}'.");
        }

        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Product URL scheme must be 'https', got '{uri.Scheme}'. Only HTTPS URLs on supported retailer sites are allowed.");
        }

        if (!AllowedHosts.Contains(uri.Host))
        {
            throw new InvalidOperationException(
                $"Product URL host '{uri.Host}' is not a supported retailer. Allowed hosts: {string.Join(", ", AllowedHosts)}.");
        }
    }
}
