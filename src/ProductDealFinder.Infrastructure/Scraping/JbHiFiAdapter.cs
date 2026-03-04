using Microsoft.Extensions.Logging;
using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Infrastructure.Scraping;

public sealed class JbHiFiAdapter : BaseProductPageAdapter
{
    public JbHiFiAdapter(ILogger<JbHiFiAdapter> logger) : base(logger) { }

    public override string RetailerCode => RetailerCodes.JbHiFi;

    protected override string? GetDefaultPriceSelector(ProductTarget target)
    {
        // JB Hi-Fi product page: main price has PriceTag_actualPrice in class or data-testid="ticket-price".
        return "[class*=\"PriceTag_actualPrice\"], [data-testid=\"ticket-price\"]";
    }
}

