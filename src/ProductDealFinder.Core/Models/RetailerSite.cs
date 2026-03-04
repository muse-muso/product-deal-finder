namespace ProductDealFinder.Core.Models;

public class RetailerSite
{
    public int Id { get; set; }

    /// <summary>
    /// Human-readable name, e.g. "JB Hi-Fi".
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Short code used to bind to scraper adapters, e.g. "JB_HIFI", "AMAZON_AU".
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Base URL, e.g. "https://www.jbhifi.com.au".
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<ProductTarget> ProductTargets { get; set; } = new List<ProductTarget>();
}

