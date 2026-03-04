namespace ProductDealFinder.Core.Models;

public class UserSettings
{
    public int Id { get; set; }

    // SMTP / email
    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    public string FromEmail { get; set; } = string.Empty;

    public string? FromDisplayName { get; set; }

    public string SmtpUserName { get; set; } = string.Empty;

    /// <summary>
    /// Key under which the SMTP password is stored in Windows Credential Manager.
    /// </summary>
    public string SmtpPasswordCredentialKey { get; set; } = string.Empty;

    // Scanning
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMinutes(60);

    public string DefaultCurrency { get; set; } = "AUD";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

