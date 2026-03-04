using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using ProductDealFinder.Core.Data;
using ProductDealFinder.Core.Models;

namespace ProductDealFinder;

public partial class ManageDataWindow : Window
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public ObservableCollection<ProductOverviewRow> ProductRows { get; } = new();
    public ObservableCollection<ScrapeHistoryRow> HistoryRows { get; } = new();
    public ObservableCollection<ScanError> ErrorRows { get; } = new();

    public ManageDataWindow(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
        InitializeComponent();

        ProductsGrid.ItemsSource = ProductRows;
        HistoryGrid.ItemsSource = HistoryRows;
        ErrorsGrid.ItemsSource = ErrorRows;

        Loaded += async (_, _) =>
        {
            try
            {
                await RefreshProductsAsync();
                await RefreshHistoryAsync();
                await RefreshErrorsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load data: " + ex.Message, "Management Window", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
    }

    private async Task RefreshProductsAsync()
    {
        ProductRows.Clear();

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var targets = await db.ProductTargets
            .Include(t => t.Product)
            .Include(t => t.RetailerSite)
            .Include(t => t.Thresholds)
            .Include(t => t.ScrapeResults)
            .OrderBy(t => t.Id)
            .ToListAsync();

        foreach (var t in targets)
        {
            var lastScrape = t.ScrapeResults
                .OrderByDescending(r => r.ScrapedAtUtc)
                .FirstOrDefault();

            var threshold = t.Thresholds
                .OrderByDescending(th => th.Id)
                .FirstOrDefault();

            ProductRows.Add(new ProductOverviewRow(
                TargetId: t.Id,
                ProductName: t.Product.Name,
                ModelNumber: t.Product.ModelNumber,
                RetailerName: t.RetailerSite.Name,
                ProductPageUrl: t.ProductPageUrl,
                LastPrice: lastScrape?.Price,
                LastScrapedAtUtc: lastScrape?.ScrapedAtUtc,
                ThresholdValue: threshold?.Value));
        }
    }

    private async Task RefreshHistoryAsync()
    {
        HistoryRows.Clear();

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var recent = await db.ScrapeResults
            .Include(r => r.ProductTarget)
            .ThenInclude(t => t.Product)
            .Include(r => r.ProductTarget)
            .ThenInclude(t => t.RetailerSite)
            .OrderByDescending(r => r.ScrapedAtUtc)
            .Take(200)
            .ToListAsync();

        foreach (var r in recent)
        {
            HistoryRows.Add(new ScrapeHistoryRow(
                Id: r.Id,
                ProductName: r.ProductTarget.Product.Name,
                RetailerName: r.ProductTarget.RetailerSite.Name,
                Price: r.Price,
                Currency: r.Currency,
                ScrapedAtUtc: r.ScrapedAtUtc));
        }
    }

    private async Task RefreshErrorsAsync()
    {
        ErrorRows.Clear();

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var recent = await db.ScanErrors
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(200)
            .ToListAsync();

        foreach (var e in recent)
        {
            ErrorRows.Add(e);
        }
    }

    private async void OnRefreshProductsClick(object sender, RoutedEventArgs e)
    {
        await RefreshProductsAsync();
    }

    private async void OnRefreshHistoryClick(object sender, RoutedEventArgs e)
    {
        await RefreshHistoryAsync();
        await RefreshErrorsAsync();
    }

    private async void OnDeleteTargetClick(object sender, RoutedEventArgs e)
    {
        if (ProductsGrid.SelectedItem is not ProductOverviewRow row)
        {
            MessageBox.Show(this, "Please select a product target first.", "Delete Target", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this, $"Delete target {row.TargetId} and all associated data?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var target = await db.ProductTargets.FindAsync(row.TargetId);
        if (target is null)
        {
            MessageBox.Show(this, "Target not found.", "Delete Target", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        db.ProductTargets.Remove(target);
        await db.SaveChangesAsync();

        await RefreshProductsAsync();
        await RefreshHistoryAsync();
        await RefreshErrorsAsync();
    }

    private async void OnUpdateThresholdClick(object sender, RoutedEventArgs e)
    {
        if (ProductsGrid.SelectedItem is not ProductOverviewRow row)
        {
            MessageBox.Show(this, "Please select a product target first.", "Update Threshold", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!decimal.TryParse(NewThresholdTextBox.Text, out var value))
        {
            MessageBox.Show(this, "New threshold must be a valid number.", "Update Threshold", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var threshold = await db.PriceThresholds
            .Where(th => th.ProductTargetId == row.TargetId && th.IsActive)
            .OrderByDescending(th => th.Id)
            .FirstOrDefaultAsync();

        if (threshold is null)
        {
            MessageBox.Show(this, "No active threshold found for this target.", "Update Threshold",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        threshold.Value = value;
        await db.SaveChangesAsync();

        await RefreshProductsAsync();
    }

    private async void OnDeleteScrapeClick(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not ScrapeHistoryRow row)
        {
            MessageBox.Show(this, "Please select a scrape entry first.", "Delete Scrape", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var entity = await db.ScrapeResults.FindAsync(row.Id);
        if (entity is null)
        {
            MessageBox.Show(this, "Scrape entry not found.", "Delete Scrape", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        db.ScrapeResults.Remove(entity);
        await db.SaveChangesAsync();

        await RefreshHistoryAsync();
    }

    private async void OnDeleteErrorClick(object sender, RoutedEventArgs e)
    {
        if (ErrorsGrid.SelectedItem is not ScanError error)
        {
            MessageBox.Show(this, "Please select an error first.", "Delete Error", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var entity = await db.ScanErrors.FindAsync(error.Id);
        if (entity is null)
        {
            MessageBox.Show(this, "Error entry not found.", "Delete Error", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        db.ScanErrors.Remove(entity);
        await db.SaveChangesAsync();

        await RefreshErrorsAsync();
    }
}

public sealed record ProductOverviewRow(
    int TargetId,
    string ProductName,
    string? ModelNumber,
    string RetailerName,
    string ProductPageUrl,
    decimal? LastPrice,
    DateTime? LastScrapedAtUtc,
    decimal? ThresholdValue);

public sealed record ScrapeHistoryRow(
    int Id,
    string ProductName,
    string RetailerName,
    decimal? Price,
    string? Currency,
    DateTime ScrapedAtUtc);

