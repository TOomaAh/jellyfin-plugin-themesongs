using Jellyfin.Plugin.ThemeSongs.Provider;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ThemeSongs
{
    /// <summary>
    /// Register tvdb services.
    /// </summary>
    public class ThemeSongServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddSingleton<PlexProvider>();
            serviceCollection.AddSingleton<TelevisionTunesProvider>();
        }
    }
}
