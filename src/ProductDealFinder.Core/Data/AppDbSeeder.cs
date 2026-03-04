using Microsoft.EntityFrameworkCore;
using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Core.Data;

public static class AppDbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        // Ensure ScanErrors table exists (e.g. if DB was created before this entity was added)
        await EnsureScanErrorsTableExistsAsync(db, cancellationToken);

        // Add new UserSettings columns if missing (existing DBs created before these properties)
        await EnsureUserSettingsColumnsAsync(db, cancellationToken);

        if (!await db.RetailerSites.AnyAsync(cancellationToken))
        {
            db.RetailerSites.AddRange(
                new RetailerSite
                {
                    Name = "JB Hi-Fi",
                    Code = RetailerCodes.JbHiFi,
                    BaseUrl = "https://www.jbhifi.com.au",
                    IsActive = true
                },
                new RetailerSite
                {
                    Name = "Amazon Australia",
                    Code = RetailerCodes.AmazonAu,
                    BaseUrl = "https://www.amazon.com.au",
                    IsActive = true
                },
                new RetailerSite
                {
                    Name = "The Good Guys",
                    Code = RetailerCodes.TheGoodGuys,
                    BaseUrl = "https://www.thegoodguys.com.au",
                    IsActive = true
                },
                new RetailerSite
                {
                    Name = "Officeworks",
                    Code = RetailerCodes.Officeworks,
                    BaseUrl = "https://www.officeworks.com.au",
                    IsActive = true
                },
                new RetailerSite
                {
                    Name = "Harvey Norman",
                    Code = RetailerCodes.HarveyNorman,
                    BaseUrl = "https://www.harveynorman.com.au",
                    IsActive = true
                },
                new RetailerSite
                {
                    Name = "Generic",
                    Code = RetailerCodes.Generic,
                    BaseUrl = string.Empty,
                    IsActive = true
                });

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureScanErrorsTableExistsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS ScanErrors (Id INTEGER PRIMARY KEY AUTOINCREMENT, ProductTargetId INTEGER, Message TEXT NOT NULL, ExceptionType TEXT, CreatedAtUtc TEXT NOT NULL)",
                cancellationToken);
        }
        catch
        {
            // Table may already exist with correct schema; ignore
        }
    }

    private static async Task EnsureUserSettingsColumnsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE UserSettings ADD COLUMN DefaultMailboxName TEXT", cancellationToken); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE UserSettings ADD COLUMN DefaultNotificationEmail TEXT", cancellationToken); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE UserSettings ADD COLUMN ExtensionRelayEnabled INTEGER", cancellationToken); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE UserSettings ADD COLUMN ExtensionRelayPort INTEGER", cancellationToken); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE UserSettings ADD COLUMN ExtensionRelaySecret TEXT", cancellationToken); } catch { }
    }
}

