namespace ProductDealFinder.Core.Models;

public class ProductTarget
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int RetailerSiteId { get; set; }
    public RetailerSite RetailerSite { get; set; } = null!;

    /// <summary>
    /// Direct URL to the product page on the retailer site.
    /// </summary>
    public string ProductPageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional CSS selector overrides for price and title, used by the generic adapter.
    /// </summary>
    public string? PriceSelectorOverride { get; set; }
    public string? TitleSelectorOverride { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<PriceThreshold> Thresholds { get; set; } = new List<PriceThreshold>();

    public ICollection<ScrapeResult> ScrapeResults { get; set; } = new List<ScrapeResult>();
}

