using Microsoft.Extensions.Logging;
using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Infrastructure.Scraping;

public sealed class OfficeworksAdapter : BaseProductPageAdapter
{
    public OfficeworksAdapter(ILogger<OfficeworksAdapter> logger) : base(logger) { }

    public override string RetailerCode => RetailerCodes.Officeworks;

    protected override string? GetDefaultPriceSelector(ProductTarget target)
    {
        // Officeworks product page: price in div with UnitPrice in class.
        return "div[class*=\"UnitPrice\"]";
    }
}

