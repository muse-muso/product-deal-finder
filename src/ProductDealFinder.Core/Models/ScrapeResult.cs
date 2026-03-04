namespace ProductDealFinder.Core.Models;

public class ScrapeResult
{
    public int Id { get; set; }

    public int ProductTargetId { get; set; }
    public ProductTarget ProductTarget { get; set; } = null!;

    public decimal? Price { get; set; }

    public string? Currency { get; set; }

    public bool InStock { get; set; }

    public string? RawPriceText { get; set; }

    public DateTime ScrapedAtUtc { get; set; } = DateTime.UtcNow;
}

