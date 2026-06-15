using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Totp.Services;
public sealed class TotpMiddleware
{
    private const string InjectedMarker = "jellyfin-totp-client";
    private static readonly Lazy<string> ClientScript = new(LoadClientScript);
    private readonly RequestDelegate _next;
    private readonly ILogger<TotpMiddleware> _logger;

    public TotpMiddleware(RequestDelegate next, ILogger<TotpMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, TotpService totp)
    {
        if (IsDashboardHtmlRequest(context))
        {
            await InjectClientScriptAsync(context);
            return;
        }

        if (!IsUserPasswordAuthenticationRequest(context))
        {
            await _next(context);
            return;
        }

        await HandleUserPasswordAuthenticationAsync(context, totp);
    }

    private async Task HandleUserPasswordAuthenticationAsync(HttpContext context, TotpService totp)
    {
        var code = context.Request.Headers["X-Jellyfin-TOTP"].FirstOrDefault();
        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            context.Response.Body = original;
            _logger.LogError(ex, "Jellyfin password authentication failed before TOTP validation could run.");
            throw;
        }

        buffer.Position = 0;
        var body = await new StreamReader(buffer).ReadToEndAsync();
        context.Response.Body = original;

        if (context.Response.StatusCode < 200 || context.Response.StatusCode >= 300)
        {
            _logger.LogDebug("Skipping TOTP validation because Jellyfin password authentication returned status {StatusCode}.", context.Response.StatusCode);
            await context.Response.WriteAsync(body);
            return;
        }

        var match = Regex.Match(body, "\"Id\"\\s*:\\s*\"(?<id>[0-9a-fA-F-]{36})\"");
        if (!match.Success || !Guid.TryParse(match.Groups["id"].Value, out var userId))
        {
            _logger.LogWarning("Unable to run TOTP validation because the Jellyfin authentication response did not contain a valid user id.");
            await context.Response.WriteAsync(body);
            return;
        }

        var required = Plugin.Instance?.Configuration.RequireTwoFactorForAllUsers == true;
        var enabled = totp.IsEnabled(userId);
        if (enabled && !totp.VerifyUser(userId, code))
        {
            _logger.LogWarning("Rejecting authentication for user {UserId}: TOTP is enabled but the submitted code was missing or invalid.", userId);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { Error = "TwoFactorRequired" });
            return;
        }

        if (required && !enabled)
        {
            _logger.LogWarning("Rejecting authentication for user {UserId}: TOTP setup is required but the user has not enabled TOTP.", userId);
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { Error = "TwoFactorSetupRequired" });
            return;
        }

        _logger.LogDebug("TOTP validation passed for user {UserId}. Enabled: {Enabled}; globally required: {Required}.", userId, enabled, required);
        context.Response.ContentLength = null;
        await context.Response.WriteAsync(body);
    }

    private async Task InjectClientScriptAsync(HttpContext context)
    {
        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            context.Response.Body = original;
            _logger.LogError(ex, "Dashboard request failed before the TOTP client script could be injected.");
            throw;
        }

        buffer.Position = 0;
        var body = await new StreamReader(buffer).ReadToEndAsync();
        context.Response.Body = original;

        if (context.Response.StatusCode < 200 || context.Response.StatusCode >= 300)
        {
            _logger.LogDebug("Skipping TOTP client script injection because dashboard request returned status {StatusCode}.", context.Response.StatusCode);
            await context.Response.WriteAsync(body);
            return;
        }

        if (body.Contains(InjectedMarker, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogTrace("Skipping TOTP client script injection because the script marker is already present.");
            await context.Response.WriteAsync(body);
            return;
        }

        var script = $"<script id=\"{InjectedMarker}\">\n{ClientScript.Value}\n</script>";
        var index = body.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        var injected = index >= 0 ? body.Insert(index, script) : body + script;
        context.Response.ContentLength = null;
        await context.Response.WriteAsync(injected);
        _logger.LogDebug("Injected TOTP client script into Jellyfin dashboard response.");
    }

    private static bool IsUserPasswordAuthenticationRequest(HttpContext context) =>
        context.Request.Path.Value?.Contains("/Users/AuthenticateByName", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsDashboardHtmlRequest(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method)) return false;
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase) && !(path.Equals("/web/", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/web", StringComparison.OrdinalIgnoreCase))) return false;
        var accept = context.Request.Headers.Accept.ToString();
        return string.IsNullOrEmpty(accept) || accept.Contains("text/html", StringComparison.OrdinalIgnoreCase) || accept.Contains("*/*", StringComparison.OrdinalIgnoreCase);
    }

    private static string LoadClientScript()
    {
        var assembly = typeof(TotpMiddleware).Assembly;
        const string resourceName = "Jellyfin.Plugin.Totp.Web.totpclient.js";
        using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
