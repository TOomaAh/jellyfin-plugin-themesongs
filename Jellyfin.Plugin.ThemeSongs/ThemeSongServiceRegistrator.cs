using Jellyfin.Plugin.ThemeSongs.Providers;
using Jellyfin.Plugin.ThemeSongs.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace Jellyfin.Plugin.ThemeSongs
{
    /// <summary>
    /// Register Theme Songs services.
    /// </summary>
    public class ThemeSongServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            // Register core services
            serviceCollection.AddSingleton<IHttpClientService, HttpClientService>();
            serviceCollection.AddSingleton<IAudioNormalizationService, AudioNormalizationService>();

            // Register providers
            serviceCollection.AddSingleton<IThemeSongProvider, PlexProvider>();
            serviceCollection.AddSingleton<IThemeSongProvider, TelevisionTunesProvider>();

            // Register download service with factory to inject cache path
            serviceCollection.AddSingleton<IThemeSongDownloadService>(serviceProvider =>
            {
                var libraryManager = serviceProvider.GetRequiredService<MediaBrowser.Controller.Library.ILibraryManager>();
                var httpClientService = serviceProvider.GetRequiredService<IHttpClientService>();
                var audioNormalizationService = serviceProvider.GetRequiredService<IAudioNormalizationService>();
                var providers = serviceProvider.GetServices<IThemeSongProvider>();
                var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ThemeSongDownloadService>>();
                var serverPaths = serviceProvider.GetRequiredService<IServerApplicationPaths>();
                var cachePath = Path.Combine(serverPaths.CachePath, "ThemeSongs");

                return new ThemeSongDownloadService(
                    libraryManager,
                    httpClientService,
                    audioNormalizationService,
                    providers,
                    logger,
                    cachePath);
            });

            // Register legacy manager
            serviceCollection.AddSingleton<ThemeSongsManager>();
        }
    }
}
