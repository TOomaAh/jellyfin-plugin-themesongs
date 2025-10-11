using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ThemeSongs.Providers;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.ThemeSongs.Services
{
    public class ThemeSongDownloadService : IThemeSongDownloadService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IHttpClientService _httpClientService;
        private readonly IAudioNormalizationService _audioNormalizationService;
        private readonly IEnumerable<IThemeSongProvider> _providers;
        private readonly ILogger<ThemeSongDownloadService> _logger;
        private readonly string _cachePath;

        public ThemeSongDownloadService(
            ILibraryManager libraryManager,
            IHttpClientService httpClientService,
            IAudioNormalizationService audioNormalizationService,
            IEnumerable<IThemeSongProvider> providers,
            ILogger<ThemeSongDownloadService> logger,
            string cachePath)
        {
            _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
            _httpClientService = httpClientService ?? throw new ArgumentNullException(nameof(httpClientService));
            _audioNormalizationService = audioNormalizationService ?? throw new ArgumentNullException(nameof(audioNormalizationService));
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cachePath = cachePath ?? throw new ArgumentNullException(nameof(cachePath));
        }

        public async Task DownloadAllThemeSongsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting theme song download for all series");

            var seriesList = GetSeriesFromLibrary();
            int processedCount = 0;
            int successCount = 0;

            foreach (var series in seriesList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Theme song download cancelled after processing {ProcessedCount} series", processedCount);
                    break;
                }

                try
                {
                    bool success = await DownloadThemeSongForSeriesAsync(series, cancellationToken);
                    if (success)
                    {
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error downloading theme song for {SeriesName}", series.Name);
                }

                processedCount++;
            }

            _logger.LogInformation("Theme song download completed. Processed {ProcessedCount} series, {SuccessCount} successful downloads",
                processedCount, successCount);
        }

        public async Task<bool> DownloadThemeSongForSeriesAsync(Series series, CancellationToken cancellationToken = default)
        {
            if (series == null)
            {
                throw new ArgumentNullException(nameof(series));
            }

            // Check if series already has theme songs
            if (series.GetThemeSongs().Any())
            {
                _logger.LogDebug("Series {SeriesName} already has theme songs, skipping", series.Name);
                return false;
            }

            var tvdbId = series.GetProviderId(MetadataProvider.Tvdb);
            if (string.IsNullOrEmpty(tvdbId))
            {
                _logger.LogDebug("Series {SeriesName} has no TVDb ID, skipping", series.Name);
                return false;
            }

            _logger.LogInformation("Processing theme song download for {SeriesName}", series.Name);

            try
            {
                // Try to find theme song URL from providers
                string themeUrl = await FindThemeSongUrlAsync(series, cancellationToken);
                if (string.IsNullOrEmpty(themeUrl))
                {
                    _logger.LogInformation("No theme song found for {SeriesName}", series.Name);
                    return false;
                }

                // Download and process the theme song
                return await DownloadAndProcessThemeSongAsync(series, themeUrl, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing theme song for {SeriesName}", series.Name);
                return false;
            }
        }

        private async Task<string> FindThemeSongUrlAsync(Series series, CancellationToken cancellationToken)
        {
            var enabledProviders = GetEnabledProviders();
            var orderedProviders = enabledProviders.OrderBy(p => p.Priority);

            if (!orderedProviders.Any())
            {
                _logger.LogWarning("No providers are enabled in configuration");
                return null;
            }

            foreach (var provider in orderedProviders)
            {
                try
                {
                    _logger.LogDebug("Trying provider {ProviderName} (priority {Priority}) for {SeriesName}",
                        provider.Name, provider.Priority, series.Name);

                    string url = await provider.GetThemeSongUrlAsync(series, cancellationToken);
                    if (!string.IsNullOrEmpty(url))
                    {
                        _logger.LogInformation("Found theme song URL for {SeriesName} using provider {ProviderName}",
                            series.Name, provider.Name);
                        return url;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Provider {ProviderName} failed for {SeriesName}",
                        provider.Name, series.Name);
                }
            }

            return null;
        }

        private IEnumerable<IThemeSongProvider> GetEnabledProviders()
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null)
            {
                _logger.LogWarning("Plugin configuration is not available, using all providers");
                return _providers;
            }

            var enabledProviders = new List<IThemeSongProvider>();

            foreach (var provider in _providers)
            {
                bool isEnabled = provider.Name switch
                {
                    "Plex" => config.EnablePlexProvider,
                    "TelevisionTunes" => config.EnableTelevisionTunesProvider,
                    _ => true // Enable unknown providers by default
                };

                if (isEnabled)
                {
                    enabledProviders.Add(provider);
                    _logger.LogDebug("Provider {ProviderName} is enabled (priority {Priority})",
                        provider.Name, provider.Priority);
                }
                else
                {
                    _logger.LogDebug("Provider {ProviderName} is disabled in configuration", provider.Name);
                }
            }

            return enabledProviders;
        }

        private async Task<bool> DownloadAndProcessThemeSongAsync(Series series, string themeUrl, CancellationToken cancellationToken)
        {
            string tempFilePath = null;
            string finalFilePath = Path.Combine(series.Path, "theme.mp3");

            try
            {
                // Ensure cache directory exists
                Directory.CreateDirectory(_cachePath);

                // Generate temporary file path
                tempFilePath = Path.Combine(_cachePath, $"{series.Name}_{series.GetProviderId(MetadataProvider.Tvdb)}.mp3");

                // Download the file
                _logger.LogInformation("Downloading theme song for {SeriesName} from {Url} to {TempPath}",
                    series.Name, themeUrl, tempFilePath);

                bool downloadSuccess = await _httpClientService.DownloadFileAsync(themeUrl, tempFilePath, cancellationToken);
                if (!downloadSuccess)
                {
                    _logger.LogError("Failed to download theme song for {SeriesName}", series.Name);
                    return false;
                }

                // Process audio normalization if enabled
                string processedFilePath = tempFilePath;
                if (Plugin.Instance?.Configuration?.NormalizeAudio == true)
                {
                    _logger.LogDebug("Normalizing audio for {SeriesName}", series.Name);
                    processedFilePath = await _audioNormalizationService.NormalizeAudioAsync(tempFilePath);

                    if (string.IsNullOrEmpty(processedFilePath))
                    {
                        _logger.LogError("Audio normalization failed for {SeriesName}", series.Name);
                        return false;
                    }
                }

                // Ensure the series directory exists before moving the file
                var seriesDir = Path.GetDirectoryName(finalFilePath);
                if (!string.IsNullOrEmpty(seriesDir))
                {
                    Directory.CreateDirectory(seriesDir);
                }

                // Move the final file to the series directory
                File.Move(processedFilePath, finalFilePath, true);

                // Clean up temporary files
                CleanupTempFiles(tempFilePath, processedFilePath);

                _logger.LogInformation("Successfully downloaded and processed theme song for {SeriesName} in path {path}", series.Name, seriesDir);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading and processing theme song for {SeriesName}", series.Name);

                // Clean up any temporary files on error
                CleanupTempFiles(tempFilePath, null);

                return false;
            }
        }

        private void CleanupTempFiles(string tempFilePath, string processedFilePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }

                if (!string.IsNullOrEmpty(processedFilePath) &&
                    processedFilePath != tempFilePath &&
                    File.Exists(processedFilePath))
                {
                    File.Delete(processedFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up temporary files");
            }
        }

        private IEnumerable<Series> GetSeriesFromLibrary()
        {
            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Series },
                IsVirtualItem = false,
                Recursive = true,
                HasTvdbId = true
            }).Cast<Series>();
        }
    }
}