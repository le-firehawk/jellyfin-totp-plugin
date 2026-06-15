using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Totp.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Totp;
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static Plugin? Instance { get; private set; }
    public override string Name => "TOTP Two-Factor Authentication";
    public override Guid Id => Guid.Parse("65e3f94b-29d8-4d3b-a348-2343784b1db8");
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer) => Instance = this;
    public IEnumerable<PluginPageInfo> GetPages() => new[] {
        new PluginPageInfo { Name = Name, EmbeddedResourcePath = GetType().Namespace + ".Web.config.html" }
    };
}
