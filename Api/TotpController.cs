using Jellyfin.Plugin.Totp.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Totp.Api;
[ApiController]
[Route("Totp")]
[Authorize]
public class TotpController : ControllerBase
{
    private readonly IUserManager _users; private readonly TotpService _totp;
    public TotpController(IUserManager users, TotpService totp) { _users = users; _totp = totp; }

    [HttpGet("ClientScript")]
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public ContentResult ClientScript()
    {
        return Content(TotpMiddleware.GetClientScript(), "application/javascript");
    }
    [HttpGet("Status/{userId:guid}")]
    public ActionResult<object> Status(Guid userId) => Ok(new { enabled = _totp.IsEnabled(userId), required = Plugin.Instance?.Configuration.RequireTwoFactorForAllUsers == true });
    [HttpPost("Setup/{userId:guid}")]
    public ActionResult<object> Setup(Guid userId)
    {
        var user = _users.GetUserById(userId); if (user is null) return NotFound();
        var secret = _totp.BeginSetup(userId, user.Username);
        return Ok(new { secret, uri = TotpService.OtpAuthUri(Plugin.Instance?.Configuration.Issuer ?? "Jellyfin", user.Username, secret) });
    }
    [HttpPost("Confirm/{userId:guid}")]
    public ActionResult Confirm(Guid userId, [FromBody] CodeRequest request) => _totp.Confirm(userId, request.Code) ? NoContent() : BadRequest("Invalid TOTP code.");
    [HttpDelete("Reset/{userId:guid}")]
    [Authorize(Policy = "RequiresElevation")]
    public ActionResult Reset(Guid userId) { _totp.Reset(userId); return NoContent(); }
}
public sealed class CodeRequest { public string Code { get; set; } = string.Empty; }
