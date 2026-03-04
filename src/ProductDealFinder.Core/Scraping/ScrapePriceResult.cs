using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Core.Scraping;

public sealed record ScrapePriceResult(
    ProductTarget ProductTarget,
    decimal? Price,
    string? Currency,
    bool InStock,
    string? RawPriceText,
    DateTime ScrapedAtUtc);

