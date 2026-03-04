using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Infrastructure.Scraping;

public sealed class OfficeworksAdapter : BaseProductPageAdapter
{
    public override string RetailerCode => RetailerCodes.Officeworks;

    protected override string? GetDefaultPriceSelector(ProductTarget target)
    {
        // Best-effort placeholder selector for Officeworks product detail pages.
        return ".price, [data-testid='product-price'], [itemprop='price']";
    }
}

