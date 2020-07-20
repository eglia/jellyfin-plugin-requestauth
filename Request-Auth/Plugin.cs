using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Plugins;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Jellyfin.Plugin.Request_Auth
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public static Plugin Instance { get; private set; }
        public static ILogger Logger{get; private set;}
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer){
            Instance = this;
        }

        public override string Name => "Request-Auth";
        public override Guid Id => Guid.Parse("16613135-721D-4A39-9396-46D80939CCBB");

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = this.Name,
                    EmbeddedResourcePath = $"{GetType().Namespace}.Config.configPage.html"
                }
            };
        }
    }
}
