using Microsoft.Extensions.Logging;
using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Infrastructure.Scraping;

public sealed class TheGoodGuysAdapter : BaseProductPageAdapter
{
    public TheGoodGuysAdapter(ILogger<TheGoodGuysAdapter> logger) : base(logger) { }

    public override string RetailerCode => RetailerCodes.TheGoodGuys;

    protected override string? GetDefaultPriceSelector(ProductTarget target)
    {
        // Best-effort placeholder; expect to refine via overrides or later tuning.
        return ".price, [data-testid='product-price'], [itemprop='price']";
    }
}

