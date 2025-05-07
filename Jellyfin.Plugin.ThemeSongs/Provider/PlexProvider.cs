using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeSongs.Provider
{
    public class PlexProvider(ILogger<PlexProvider> logger) : IProvider
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _baseUrl = "http://tvthemes.plexapp.com/";
        private readonly ILogger<PlexProvider> _logger = logger;

        public Task<string> GetURL(Series serie, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting URL for {name}", serie.Name);
            var tvdb = serie.GetProviderId(MetadataProvider.Tvdb);
            var link = $"{_baseUrl}/{tvdb}.mp3";
            // make head request to check if the file exists
            var request = new HttpRequestMessage(HttpMethod.Head, link);
            return _httpClient.SendAsync(request, cancellationToken).ContinueWith(task =>
            {
                if (task.Result.IsSuccessStatusCode)
                {
                    return link;
                }
                else
                {
                    throw new FileNotFoundException("File not found", link);
                }
            }, cancellationToken);
        }
    }
}