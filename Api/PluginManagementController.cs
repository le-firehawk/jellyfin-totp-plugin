using MediaBrowser.Common.Plugins;
using MediaBrowser.Common.Updates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Totp.Api;

/// <summary>
/// Provides plugin-management compatibility routes for Jellyfin deployments where the built-in plugin routes are not matched.
/// </summary>
[ApiController]
[Route("Plugins/{pluginId:guid}/{version}")]
[Authorize(Policy = "RequiresElevation")]
public sealed class PluginManagementController : ControllerBase
{
    private readonly IPluginManager _pluginManager;
    private readonly IInstallationManager _installationManager;

    public PluginManagementController(IPluginManager pluginManager, IInstallationManager installationManager)
    {
        _pluginManager = pluginManager;
        _installationManager = installationManager;
    }

    [HttpPost("Disable")]
    public ActionResult Disable(Guid pluginId, string version)
    {
        if (!TryGetTotpPlugin(pluginId, version, out var plugin))
        {
            return NotFound();
        }

        _pluginManager.DisablePlugin(plugin);
        return NoContent();
    }

    [HttpPost("Enable")]
    public ActionResult Enable(Guid pluginId, string version)
    {
        if (!TryGetTotpPlugin(pluginId, version, out var plugin))
        {
            return NotFound();
        }

        _pluginManager.EnablePlugin(plugin);
        return NoContent();
    }

    [HttpDelete]
    public ActionResult Uninstall(Guid pluginId, string version)
    {
        if (!TryGetTotpPlugin(pluginId, version, out var plugin))
        {
            return NotFound();
        }

        _installationManager.UninstallPlugin(plugin);
        return NoContent();
    }

    private bool TryGetTotpPlugin(Guid pluginId, string version, out LocalPlugin plugin)
    {
        plugin = null!;
        var currentPlugin = Plugin.Instance;
        if (currentPlugin is null || pluginId != currentPlugin.Id || !Version.TryParse(version, out var parsedVersion))
        {
            return false;
        }

        var localPlugin = _pluginManager.GetPlugin(pluginId, parsedVersion);
        if (localPlugin is null)
        {
            return false;
        }

        plugin = localPlugin;
        return true;
    }
}
