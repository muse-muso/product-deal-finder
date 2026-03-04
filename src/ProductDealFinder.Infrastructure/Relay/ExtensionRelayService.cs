using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using CredentialManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProductDealFinder.Core.Data;
using ProductDealFinder.Core.Email;
using ProductDealFinder.Core.Models;

namespace ProductDealFinder.Infrastructure.Relay;

public sealed class ExtensionRelayService : IExtensionRelayService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExtensionRelayService> _logger;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    /// <summary>
    /// Windows Credential Manager key under which the relay shared secret is stored.
    /// The secret is auto-generated on first use and never stored in plain text on disk.
    /// </summary>
    internal const string RelaySecretCredentialKey = "ProductDealFinder_RelaySecret";

    /// <summary>Cached secret for the lifetime of this relay instance.</summary>
    private string? _relaySecret;

    // Rate limiting: at most 10 alert requests per minute to prevent email flooding.
    private const int MaxAlertsPerMinute = 10;
    private readonly object _rateLimitLock = new();
    private readonly Queue<DateTime> _alertTimestamps = new();

    // Maximum allowed request body size (64 KB) to prevent memory exhaustion.
    private const long MaxBodyBytes = 65_536;

    public ExtensionRelayService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExtensionRelayService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken).ConfigureAwait(false);

        int port;
        bool enabled;
        UserSettings? settings;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
            settings = await db.UserSettings.OrderByDescending(s => s.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (settings == null || settings.ExtensionRelayEnabled != true)
            {
                _logger.LogInformation("Extension relay is disabled or no settings; not starting listener.");
                return;
            }
            port = (settings.ExtensionRelayPort > 0 ? settings.ExtensionRelayPort : null) ?? 8765;
            enabled = true;
        }

        if (!enabled) return;

        // Auto-generate and cache the shared secret. Migrates any existing secret from DB to
        // Windows Credential Manager on first run so existing users don't need to re-pair.
        _relaySecret = EnsureAndGetSecret(settings);

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        try
        {
            _listener.Start();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Extension relay failed to start on port {Port}. Is it in use?", port);
            _listener = null;
            return;
        }

        _cts = new CancellationTokenSource();
        _listenTask = ListenAsync(_cts.Token);
        _logger.LogInformation("Extension relay listening on http://127.0.0.1:{Port}/", port);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        _listener?.Stop();
        _listener?.Close();
        _listener = null;

        if (_listenTask != null)
        {
            try { await _listenTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
            _listenTask = null;
        }
    }

    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken).ConfigureAwait(false);
        await StartAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (_listener != null && _listener.IsListening && !ct.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync().WaitAsync(ct).ConfigureAwait(false);
                _ = HandleRequestAsync(context);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Extension relay accept error.");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // --- /pair endpoint: returns the shared secret so the extension can auto-configure. ---
        // This is localhost-only (listener is bound to 127.0.0.1), so only local processes can
        // reach it. Any local process running as the same user can already read Credential Manager,
        // so this adds no new attack surface beyond what is already accessible locally.
        if (request.HttpMethod == "GET" && request.Url?.AbsolutePath?.TrimEnd('/') == "/pair")
        {
            var secret = _relaySecret ?? EnsureAndGetSecret(null);
            var pairData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = true, secret }));
            response.StatusCode = 200;
            response.ContentType = "application/json";
            response.ContentLength64 = pairData.Length;
            await response.OutputStream.WriteAsync(pairData).ConfigureAwait(false);
            response.Close();
            return;
        }

        // Only POST /alert is accepted for all other requests.
        if (request.HttpMethod != "POST" || request.Url?.AbsolutePath?.TrimEnd('/') != "/alert")
        {
            response.StatusCode = 404;
            response.Close();
            return;
        }

        // Content-Type must be application/json.
        if (!string.Equals(
                request.ContentType?.Split(';')[0].Trim(),
                "application/json",
                StringComparison.OrdinalIgnoreCase))
        {
            response.StatusCode = 415;
            response.Close();
            return;
        }

        // Reject oversized bodies to prevent memory exhaustion.
        if (request.ContentLength64 > MaxBodyBytes)
        {
            response.StatusCode = 413;
            response.Close();
            return;
        }

        // Rate limit: max MaxAlertsPerMinute alerts per 60-second sliding window.
        lock (_rateLimitLock)
        {
            var now = DateTime.UtcNow;
            while (_alertTimestamps.Count > 0 && (now - _alertTimestamps.Peek()).TotalSeconds > 60)
                _alertTimestamps.Dequeue();
            if (_alertTimestamps.Count >= MaxAlertsPerMinute)
            {
                response.StatusCode = 429;
                response.Close();
                return;
            }
            _alertTimestamps.Enqueue(now);
        }

        // Authenticate using the shared secret (always required).
        var requiredSecret = _relaySecret ?? EnsureAndGetSecret(null);
        var providedSecret = request.Headers["X-Extension-Secret"];
        if (providedSecret != requiredSecret)
        {
            response.StatusCode = 401;
            response.Close();
            return;
        }

        string? bodyText = null;
        try
        {
            // Read body up to MaxBodyBytes; discard any excess to avoid allocating more memory.
            using var limitedStream = new LengthLimitedStream(request.InputStream, MaxBodyBytes);
            using var reader = new StreamReader(limitedStream, request.ContentEncoding ?? Encoding.UTF8);
            bodyText = await reader.ReadToEndAsync().ConfigureAwait(false);
        }
        catch
        {
            response.StatusCode = 400;
            response.Close();
            return;
        }

        ExtensionAlertPayload? payload;
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            payload = JsonSerializer.Deserialize<ExtensionAlertPayload>(bodyText, options);
        }
        catch
        {
            response.StatusCode = 400;
            response.Close();
            return;
        }

        if (payload == null || string.IsNullOrWhiteSpace(payload.Url))
        {
            response.StatusCode = 400;
            response.Close();
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var settings = await db.UserSettings.OrderByDescending(s => s.Id).FirstOrDefaultAsync().ConfigureAwait(false);

        if (settings == null || settings.ExtensionRelayEnabled != true)
        {
            response.StatusCode = 503;
            response.Close();
            return;
        }

        var emailService = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();
        var dto = new ExtensionAlertPayload(
            payload.ProductName ?? "Product",
            payload.RetailerName ?? "",
            payload.Url,
            payload.Price,
            payload.Threshold,
            payload.Currency ?? "AUD");

        try
        {
            await emailService.SendExtensionPriceAlertAsync(dto).ConfigureAwait(false);
            response.StatusCode = 200;
            response.ContentType = "application/json";
            var ok = Encoding.UTF8.GetBytes("{\"ok\":true}");
            response.ContentLength64 = ok.Length;
            await response.OutputStream.WriteAsync(ok).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extension relay failed to send email for {ProductName}", dto.ProductName);
            response.StatusCode = 500;
        }

        response.Close();
    }

    /// <summary>
    /// Returns the relay secret from Windows Credential Manager, creating and persisting a new
    /// random secret if one does not exist. Migrates any existing plain-text secret from the
    /// UserSettings DB row into Credential Manager on first call (preserves existing pairings).
    /// </summary>
    private static string EnsureAndGetSecret(UserSettings? settings)
    {
        using var existingCred = new Credential { Target = RelaySecretCredentialKey, Type = CredentialType.Generic };
        if (existingCred.Load())
            return existingCred.Password;

        // Migrate from DB column, or generate a new GUID secret.
        var secret = !string.IsNullOrWhiteSpace(settings?.ExtensionRelaySecret)
            ? settings.ExtensionRelaySecret
            : Guid.NewGuid().ToString("N");

        using var newCred = new Credential
        {
            Target = RelaySecretCredentialKey,
            Username = "relay",
            Password = secret,
            PersistanceType = PersistanceType.LocalComputer,
            Type = CredentialType.Generic
        };
        newCred.Save();
        return secret;
    }

    /// <summary>
    /// Stream wrapper that caps reads to a maximum number of bytes.
    /// </summary>
    private sealed class LengthLimitedStream : Stream
    {
        private readonly Stream _inner;
        private long _remaining;

        public LengthLimitedStream(Stream inner, long maxBytes)
        {
            _inner = inner;
            _remaining = maxBytes;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining <= 0) return 0;
            var toRead = (int)Math.Min(count, _remaining);
            var n = _inner.Read(buffer, offset, toRead);
            _remaining -= n;
            return n;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            if (_remaining <= 0) return 0;
            var toRead = (int)Math.Min(count, _remaining);
            var n = await _inner.ReadAsync(buffer, offset, toRead, ct).ConfigureAwait(false);
            _remaining -= n;
            return n;
        }
    }
}
