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

    /// <summary>
    /// Optional label for this mailbox (e.g. "Gmail", "Work") for display.
    /// </summary>
    public string? DefaultMailboxName { get; set; }

    /// <summary>
    /// Email address to send notifications to. If null or empty, alerts are sent to FromEmail.
    /// </summary>
    public string? DefaultNotificationEmail { get; set; }

    public string SmtpUserName { get; set; } = string.Empty;

    /// <summary>
    /// Key under which the SMTP password is stored in Windows Credential Manager.
    /// </summary>
    public string SmtpPasswordCredentialKey { get; set; } = string.Empty;

    // Scanning
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMinutes(60);

    public string DefaultCurrency { get; set; } = "AUD";

    /// <summary>
    /// When true, the app listens for price alerts from the Firefox extension and sends email via SMTP.
    /// Null when the column was added by ALTER to an existing DB (existing rows).
    /// </summary>
    public bool? ExtensionRelayEnabled { get; set; }

    /// <summary>
    /// Port for the extension relay HTTP listener (default 8765).
    /// Null when the column was added by ALTER to an existing DB (existing rows).
    /// </summary>
    public int? ExtensionRelayPort { get; set; } = 8765;

    /// <summary>
    /// Optional secret token; extension must send it in X-Extension-Secret header for requests to be accepted.
    /// </summary>
    public string? ExtensionRelaySecret { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

