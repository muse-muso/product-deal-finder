namespace ProductDealFinder.Core.Models;

public enum ThresholdComparison
{
    LessThan,
    LessThanOrEqual
}

public class PriceThreshold
{
    public int Id { get; set; }

    public int ProductTargetId { get; set; }
    public ProductTarget ProductTarget { get; set; } = null!;

    public decimal Value { get; set; }

    public string Currency { get; set; } = "AUD";

    public ThresholdComparison Comparison { get; set; } = ThresholdComparison.LessThanOrEqual;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
}

