namespace Jellyfin.Plugin.Request_Auth
{
    public class PluginConfiguration :  MediaBrowser.Model.Plugins.BasePluginConfiguration
    {
        public string RequestURL { get; set; }

        public PluginConfiguration()
        {
            RequestURL = "https://example.com/auth";
        }
    }
}
