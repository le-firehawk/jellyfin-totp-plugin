using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.Totp.Services;
public sealed class TotpMiddleware
{
    private readonly RequestDelegate _next;
    public TotpMiddleware(RequestDelegate next) => _next = next;
    public async Task InvokeAsync(HttpContext context, TotpService totp)
    {
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
}
