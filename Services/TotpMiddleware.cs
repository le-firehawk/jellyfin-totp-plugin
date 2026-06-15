using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.Totp.Services;
public sealed class TotpMiddleware
{
    private const string InjectedMarker = "jellyfin-totp-client";
    private static readonly Lazy<string> ClientScript = new(LoadClientScript);
    private readonly RequestDelegate _next;
    public TotpMiddleware(RequestDelegate next) => _next = next;
    public async Task InvokeAsync(HttpContext context, TotpService totp)
    {
        if (IsDashboardHtmlRequest(context)) { await InjectClientScriptAsync(context); return; }
        if (!context.Request.Path.Value?.Contains("/Users/AuthenticateByName", StringComparison.OrdinalIgnoreCase) == true) { await _next(context); return; }
        var code = context.Request.Headers["X-Jellyfin-TOTP"].FirstOrDefault();
        var original = context.Response.Body; await using var buffer = new MemoryStream(); context.Response.Body = buffer;
        await _next(context); buffer.Position = 0; var body = await new StreamReader(buffer).ReadToEndAsync();
        if (context.Response.StatusCode < 200 || context.Response.StatusCode >= 300) { context.Response.Body = original; await context.Response.WriteAsync(body); return; }
        var match = Regex.Match(body, "\"Id\"\\s*:\\s*\"(?<id>[0-9a-fA-F-]{36})\"");
        if (!match.Success || !Guid.TryParse(match.Groups["id"].Value, out var userId)) { context.Response.Body = original; await context.Response.WriteAsync(body); return; }
        var required = Plugin.Instance?.Configuration.RequireTwoFactorForAllUsers == true;
        if (totp.IsEnabled(userId) && !totp.VerifyUser(userId, code)) { context.Response.Body = original; context.Response.StatusCode = 401; await context.Response.WriteAsJsonAsync(new { Error = "TwoFactorRequired" }); return; }
        if (required && !totp.IsEnabled(userId)) { context.Response.Body = original; context.Response.StatusCode = 403; await context.Response.WriteAsJsonAsync(new { Error = "TwoFactorSetupRequired" }); return; }
        context.Response.Body = original; context.Response.ContentLength = null; await context.Response.WriteAsync(body);
    }

    private async Task InjectClientScriptAsync(HttpContext context)
    {
        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        await _next(context);

        buffer.Position = 0;
        var body = await new StreamReader(buffer).ReadToEndAsync();
        context.Response.Body = original;

        if (context.Response.StatusCode < 200 || context.Response.StatusCode >= 300 || body.Contains(InjectedMarker, StringComparison.OrdinalIgnoreCase))
        {
            await context.Response.WriteAsync(body);
            return;
        }

        var script = $"<script id=\"{InjectedMarker}\">\n{ClientScript.Value}\n</script>";
        var index = body.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        var injected = index >= 0 ? body.Insert(index, script) : body + script;
        context.Response.ContentLength = null;
        await context.Response.WriteAsync(injected);
    }

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
