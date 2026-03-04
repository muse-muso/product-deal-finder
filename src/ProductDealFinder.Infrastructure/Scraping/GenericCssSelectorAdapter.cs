using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Infrastructure.Scraping;

public sealed class GenericCssSelectorAdapter : BaseProductPageAdapter
{
    public override string RetailerCode => RetailerCodes.Generic;

    protected override string? GetDefaultPriceSelector(ProductTarget target)
    {
        // For the generic adapter we prefer that the user provides explicit selectors.
        // If none are provided, fall back to full-page scanning in the base class.
        return null;
    }
}

