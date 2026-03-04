namespace ProductDealFinder.Core.Models;

public class ScanError
{
    public int Id { get; set; }

    public int? ProductTargetId { get; set; }
    public ProductTarget? ProductTarget { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? ExceptionType { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

