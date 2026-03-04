using Microsoft.Extensions.Logging;
using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Infrastructure.Scraping;

public sealed class HarveyNormanAdapter : BaseProductPageAdapter
{
    public HarveyNormanAdapter(ILogger<HarveyNormanAdapter> logger) : base(logger) { }

    public override string RetailerCode => RetailerCodes.HarveyNorman;

    protected override string? GetDefaultPriceSelector(ProductTarget target)
    {
        // Best-effort placeholder selector for Harvey Norman product detail pages.
        return ".price, [data-testid='product-price'], [itemprop='price']";
    }
}

