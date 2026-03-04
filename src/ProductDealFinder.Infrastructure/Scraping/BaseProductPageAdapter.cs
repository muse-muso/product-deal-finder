using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ProductDealFinder.Core.Models;
using ProductDealFinder.Core.Scraping;

namespace ProductDealFinder.Infrastructure.Scraping;

public abstract class BaseProductPageAdapter : IProductPageAdapter
{
    private const int ScrapedTextLogLimit = 2500;

    protected readonly ILogger _logger;

    protected BaseProductPageAdapter(ILogger logger)
    {
        _logger = logger;
    }

    public abstract string RetailerCode { get; }

    public async Task<ScrapePriceResult> ScrapeAsync(IPage page, ProductTarget target, CancellationToken cancellationToken)
    {
        // Try an explicit selector first (per-target override), then any adapter default,
        // and finally fall back to scanning the full page text.
        string? selector = !string.IsNullOrWhiteSpace(target.PriceSelectorOverride)
            ? target.PriceSelectorOverride
            : GetDefaultPriceSelector(target);

        string? text = null;

        if (!string.IsNullOrWhiteSpace(selector))
        {
            var locator = page.Locator(selector);
            if (await locator.CountAsync() > 0)
            {
                text = await locator.First.InnerTextAsync(new LocatorInnerTextOptions
                {
                    Timeout = 5_000
                });
            }
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogDebug("Scrape TargetId={TargetId}: no text from selector, using body", target.Id);
            text = await page.InnerTextAsync("body", new PageInnerTextOptions
            {
                Timeout = 10_000
            });
        }

        string selectorUsed = !string.IsNullOrWhiteSpace(selector) ? selector : "(body fallback)";
        int textLen = text?.Length ?? 0;
        string snippet = string.IsNullOrEmpty(text)
            ? "(empty)"
            : text.Length <= ScrapedTextLogLimit
                ? text
                : text.Substring(0, ScrapedTextLogLimit) + "... [truncated]";
        _logger.LogInformation(
            "Scrape TargetId={TargetId} Retailer={Retailer} Url={Url} Selector={Selector} TextLength={TextLength}. Scraped text snippet: {Snippet}",
            target.Id, RetailerCode, target.ProductPageUrl, selectorUsed, textLen, snippet);

        (decimal? price, string? raw) = TryExtractPrice(text);

        _logger.LogInformation(
            "Scrape TargetId={TargetId} price extraction: RawMatch={RawMatch} ParsedPrice={ParsedPrice}",
            target.Id, raw ?? "(none)", price?.ToString(CultureInfo.InvariantCulture) ?? "(null)");

        var currency = target.Thresholds.FirstOrDefault()?.Currency
                       ?? "AUD";

        return new ScrapePriceResult(
            target,
            price,
            currency,
            InStock: true,
            RawPriceText: raw,
            ScrapedAtUtc: DateTime.UtcNow);
    }

    protected virtual string? GetDefaultPriceSelector(ProductTarget target) => null;

    protected static (decimal? Price, string? RawText) TryExtractPrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, null);
        }

        // Look for currency-like patterns, e.g. $1,234.56
        var match = Regex.Match(
            text,
            @"\$?\s*(\d{1,3}(?:[,\s]\d{3})*(?:\.\d{2})|\d+(?:\.\d{2})?)",
            RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return (null, null);
        }

        string raw = match.Value;
        string numeric = match.Groups[1].Value
            .Replace(",", string.Empty)
            .Replace(" ", string.Empty);

        if (decimal.TryParse(numeric, NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return (value, raw);
        }

        return (null, raw);
    }
}

