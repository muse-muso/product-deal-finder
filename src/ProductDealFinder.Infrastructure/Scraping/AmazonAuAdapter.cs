using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Infrastructure.Scraping;

public sealed class AmazonAuAdapter : BaseProductPageAdapter
{
    public override string RetailerCode => RetailerCodes.AmazonAu;

    protected override string? GetDefaultPriceSelector(ProductTarget target)
    {
        // Amazon product pages typically expose price spans near the buy box.
        // This is a heuristic; users can override as needed.
        return "#priceblock_ourprice, #priceblock_dealprice, span.a-price span.a-offscreen";
    }
}

