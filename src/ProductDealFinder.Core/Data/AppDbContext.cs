using Microsoft.EntityFrameworkCore;
using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Core.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<RetailerSite> RetailerSites => Set<RetailerSite>();
    public DbSet<ProductTarget> ProductTargets => Set<ProductTarget>();
    public DbSet<PriceThreshold> PriceThresholds => Set<PriceThreshold>();
    public DbSet<ScrapeResult> ScrapeResults => Set<ScrapeResult>();
    public DbSet<ScanError> ScanErrors => Set<ScanError>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(p => p.ModelNumber)
                .HasMaxLength(128);

            entity.Property(p => p.SpecificationsJson)
                .HasColumnType("TEXT");
        });

        modelBuilder.Entity<RetailerSite>(entity =>
        {
            entity.Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(r => r.Code)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasIndex(r => r.Code)
                .IsUnique();

            entity.Property(r => r.BaseUrl)
                .IsRequired()
                .HasMaxLength(512);
        });

        modelBuilder.Entity<ProductTarget>(entity =>
        {
            entity.Property(t => t.ProductPageUrl)
                .IsRequired()
                .HasMaxLength(1024);

            entity.HasOne(t => t.Product)
                .WithMany(p => p.Targets)
                .HasForeignKey(t => t.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.RetailerSite)
                .WithMany(r => r.ProductTargets)
                .HasForeignKey(t => t.RetailerSiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PriceThreshold>(entity =>
        {
            entity.Property(t => t.Value)
                .HasColumnType("decimal(18,2)");

            entity.Property(t => t.Currency)
                .IsRequired()
                .HasMaxLength(8);

            entity.HasOne(t => t.ProductTarget)
                .WithMany(p => p.Thresholds)
                .HasForeignKey(t => t.ProductTargetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScrapeResult>(entity =>
        {
            entity.Property(r => r.Price)
                .HasColumnType("decimal(18,2)");

            entity.Property(r => r.Currency)
                .HasMaxLength(8);

            entity.Property(r => r.RawPriceText)
                .HasColumnType("TEXT");

            entity.HasOne(r => r.ProductTarget)
                .WithMany(t => t.ScrapeResults)
                .HasForeignKey(r => r.ProductTargetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScanError>(entity =>
        {
            entity.Property(e => e.Message)
                .IsRequired()
                .HasMaxLength(2048);

            entity.Property(e => e.ExceptionType)
                .HasMaxLength(512);

            entity.HasOne(e => e.ProductTarget)
                .WithMany()
                .HasForeignKey(e => e.ProductTargetId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UserSettings>(entity =>
        {
            entity.Property(s => s.SmtpHost)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(s => s.FromEmail)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(s => s.FromDisplayName)
                .HasMaxLength(256);

            entity.Property(s => s.DefaultMailboxName)
                .HasMaxLength(128);

            entity.Property(s => s.DefaultNotificationEmail)
                .HasMaxLength(256);

            entity.Property(s => s.SmtpUserName)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(s => s.SmtpPasswordCredentialKey)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(s => s.DefaultCurrency)
                .IsRequired()
                .HasMaxLength(8);
        });
    }
}

