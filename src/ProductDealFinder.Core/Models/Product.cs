namespace ProductDealFinder.Core.Models;

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ModelNumber { get; set; }

    /// <summary>
    /// Arbitrary specifications stored as JSON (e.g. key-value pairs).
    /// </summary>
    public string? SpecificationsJson { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ProductTarget> Targets { get; set; } = new List<ProductTarget>();
}

