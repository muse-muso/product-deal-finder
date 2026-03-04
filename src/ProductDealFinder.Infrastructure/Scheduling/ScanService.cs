using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductDealFinder.Core.Data;
using ProductDealFinder.Core.Email;
using ProductDealFinder.Core.Models;
using ProductDealFinder.Core.Scheduling;
using ProductDealFinder.Core.Scraping;

namespace ProductDealFinder.Infrastructure.Scheduling;

public class ScanService : IScanService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IScraperEngine _scraperEngine;
    private readonly IEmailNotificationService _emailNotificationService;
    private readonly ILogger<ScanService> _logger;

    public ScanService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IScraperEngine scraperEngine,
        IEmailNotificationService emailNotificationService,
        ILogger<ScanService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _scraperEngine = scraperEngine;
        _emailNotificationService = emailNotificationService;
        _logger = logger;
    }

    public async Task RunScanOnceAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var targets = await db.ProductTargets
            .Include(t => t.Product)
            .Include(t => t.RetailerSite)
            .Include(t => t.Thresholds.Where(th => th.IsActive))
            .Where(t => t.IsActive && t.Thresholds.Any(th => th.IsActive))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Starting scan run for {TargetCount} active targets", targets.Count);

        foreach (var target in targets)
        {
            try
            {
                var scrape = await _scraperEngine.ScrapeAsync(target, cancellationToken);

                var scrapeResult = new ScrapeResult
                {
                    ProductTargetId = target.Id,
                    Price = scrape.Price,
                    Currency = scrape.Currency,
                    InStock = scrape.InStock,
                    RawPriceText = scrape.RawPriceText,
                    ScrapedAtUtc = scrape.ScrapedAtUtc
                };

                db.ScrapeResults.Add(scrapeResult);
                await db.SaveChangesAsync(cancellationToken);

                if (!scrape.Price.HasValue)
                {
                    _logger.LogWarning("No price parsed for ProductTarget {TargetId}", target.Id);
                    continue;
                }

                foreach (var threshold in target.Thresholds.Where(th => th.IsActive))
                {
                    if (ShouldTrigger(threshold, scrape.Price.Value))
                    {
                        var context = new PriceAlertContext(
                            target.Product,
                            target,
                            threshold,
                            scrapeResult);

                        await _emailNotificationService.SendPriceAlertAsync(context, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while scanning ProductTarget {TargetId}", target.Id);
            }
        }

        _logger.LogInformation("Scan run completed");
    }

    private static bool ShouldTrigger(PriceThreshold threshold, decimal currentPrice)
    {
        return threshold.Comparison switch
        {
            ThresholdComparison.LessThan => currentPrice < threshold.Value,
            ThresholdComparison.LessThanOrEqual => currentPrice <= threshold.Value,
            _ => false
        };
    }
}

