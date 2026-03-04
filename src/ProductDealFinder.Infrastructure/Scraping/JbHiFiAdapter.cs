using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Infrastructure.Scraping;

public sealed class JbHiFiAdapter : BaseProductPageAdapter
{
    public override string RetailerCode => RetailerCodes.JbHiFi;

    protected override string? GetDefaultPriceSelector(ProductTarget target)
    {
        // NOTE: Selector is a best-effort placeholder and may need to be updated
        // against the live site. Users can override via ProductTarget.PriceSelectorOverride.
        return ".price, [data-testid='product-price'], [data-automation='product-price']";
    }
}

