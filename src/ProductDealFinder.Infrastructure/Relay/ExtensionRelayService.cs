using System.Net;
using System.Text;
using System.Text.Json;
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
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
            var settings = await db.UserSettings.OrderByDescending(s => s.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (settings == null || settings.ExtensionRelayEnabled != true)
            {
                _logger.LogInformation("Extension relay is disabled or no settings; not starting listener.");
                return;
            }
            port = (settings.ExtensionRelayPort > 0 ? settings.ExtensionRelayPort : null) ?? 8765;
            enabled = true;
        }

        if (!enabled) return;

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

        if (request.HttpMethod != "POST" || request.Url?.AbsolutePath?.TrimEnd('/') != "/alert")
        {
            response.StatusCode = 404;
            response.Close();
            return;
        }

        string? bodyText = null;
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
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

        if (!string.IsNullOrWhiteSpace(settings.ExtensionRelaySecret))
        {
            var secret = request.Headers["X-Extension-Secret"];
            if (secret != settings.ExtensionRelaySecret)
            {
                response.StatusCode = 401;
                response.Close();
                return;
            }
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
}
