using Microsoft.EntityFrameworkCore;
using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Core.Data;

public static class AppDbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

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
}

