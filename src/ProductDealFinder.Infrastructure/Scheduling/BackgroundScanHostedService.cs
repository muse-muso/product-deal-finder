using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductDealFinder.Core.Data;
using ProductDealFinder.Core.Models;
using ProductDealFinder.Core.Scheduling;

namespace ProductDealFinder.Infrastructure.Scheduling;

public class BackgroundScanHostedService : BackgroundService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IScanService _scanService;
    private readonly ILogger<BackgroundScanHostedService> _logger;

    public BackgroundScanHostedService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IScanService scanService,
        ILogger<BackgroundScanHostedService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _scanService = scanService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background scan service starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan interval = await GetScanIntervalAsync(stoppingToken);

            try
            {
                await _scanService.RunScanOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running scheduled scan.");
            }

            try
            {
                _logger.LogInformation("Waiting {Interval} before next scan.", interval);
                await Task.Delay(interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Swallow if we're shutting down.
            }
        }

        _logger.LogInformation("Background scan service stopping.");
    }

    private async Task<TimeSpan> GetScanIntervalAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        UserSettings? settings = await db.UserSettings
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return settings?.ScanInterval != TimeSpan.Zero
            ? settings!.ScanInterval
            : TimeSpan.FromMinutes(60);
    }
}

