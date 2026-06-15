using MediaBrowser.Model.Plugins;
namespace Jellyfin.Plugin.Totp.Configuration;
public class PluginConfiguration : BasePluginConfiguration
{
    public bool RequireTwoFactorForAllUsers { get; set; }
    public int CodeWindow { get; set; } = 1;
    public string Issuer { get; set; } = "Jellyfin";
}
