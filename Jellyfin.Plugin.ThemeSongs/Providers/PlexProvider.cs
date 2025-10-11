using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.ThemeSongs.Services;

namespace Jellyfin.Plugin.ThemeSongs.Providers
{
    public class PlexProvider : IThemeSongProvider
    {
        private readonly IHttpClientService _httpClientService;
        private readonly ILogger<PlexProvider> _logger;
        private const string BaseUrl = "http://tvthemes.plexapp.com";

        public PlexProvider(IHttpClientService httpClientService, ILogger<PlexProvider> logger)
        {
            _httpClientService = httpClientService ?? throw new ArgumentNullException(nameof(httpClientService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Name => "Plex";
        public int Priority => Plugin.Instance?.Configuration?.PlexProviderPriority ?? 1;

        public async Task<string> GetThemeSongUrlAsync(Series series, CancellationToken cancellationToken = default)
        {
            if (series == null)
            {
                throw new ArgumentNullException(nameof(series));
            }

            var tvdbId = series.GetProviderId(MetadataProvider.Tvdb);
            if (string.IsNullOrEmpty(tvdbId))
            {
                _logger.LogDebug("No TVDb ID found for series {SeriesName}", series.Name);
                return null;
            }

            var url = $"{BaseUrl}/{tvdbId}.mp3";
            _logger.LogDebug("Checking Plex provider for {SeriesName} with URL {Url}", series.Name, url);

            var exists = await _httpClientService.HeadRequestAsync(url, cancellationToken);
            if (exists)
            {
                _logger.LogInformation("Found theme song for {SeriesName} on Plex provider", series.Name);
                return url;
            }

            _logger.LogDebug("No theme song found for {SeriesName} on Plex provider", series.Name);
            return null;
        }
    }
}