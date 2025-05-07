using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ThemeSongs.Provider;
using MediaBrowser.Controller;

namespace Jellyfin.Plugin.ThemeSongs

{


    public class ThemeSongsManager : IDisposable
    {
        private readonly ILibraryManager _libraryManager;
        private readonly Timer _timer;
        private readonly ILogger<ThemeSongsManager> _logger;
        private readonly string _themeSongsTempPath;
        private readonly IProvider[] providers;

        public ThemeSongsManager(ILibraryManager libraryManager, ILogger<ThemeSongsManager> logger, IServerApplicationPaths serverApplicationPaths, PlexProvider plexProvider, TelevisionTunesProvider televisionTunesProvider)
        {
            IProvider[] providers =
            [
                televisionTunesProvider,
                plexProvider
            ];
            _libraryManager = libraryManager;
            _logger = logger;
            _themeSongsTempPath = Path.Join(serverApplicationPaths.CachePath, "ThemeSongs");
            _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
            this.providers = providers;
        }

        private IEnumerable<Series> GetSeriesFromLibrary()
        {
            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = [BaseItemKind.Series],
                IsVirtualItem = false,
                Recursive = true,
                HasTvdbId = true
            }).Select(m => m as Series);
        }


        public void DownloadAllThemeSongs()
        {
            HttpClientHandler handler = new()
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
            var series = GetSeriesFromLibrary();
            foreach (var serie in series)
            {
                if (serie.GetThemeSongs().Count() == 0)
                {
                    var themeSongPath = Path.Join(serie.Path, "theme.mp3");
                    Directory.CreateDirectory(_themeSongsTempPath);
                    var themeSongTempFile = Path.Join(_themeSongsTempPath, $"{serie.Name + "_" + serie.GetProviderId(MetadataProvider.Tvdb)}.mp3");

                    var link = string.Empty;

                    foreach (var provider in providers)
                    {
                        try
                        {
                            link = provider.GetURL(serie, CancellationToken.None).Result;
                            if (!string.IsNullOrEmpty(link))
                            {
                                break;
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.LogDebug("{seriesName} theme song not found with {provider} {exception}", serie.Name, provider.GetType().Name, e.Message);
                        }
                    }

                    if (string.IsNullOrEmpty(link))
                    {
                        _logger.LogInformation("{seriesName} theme song not found", serie.Name);
                        continue;
                    }


                    try
                    {
                        _logger.LogInformation("Trying to download {seriesName} theme song from {link} to {path}", serie.Name, link, themeSongTempFile);
                        using var client = new HttpClient(handler);
                        using var response = client.GetAsync(link).Result;
                        using var fileStream = new FileStream(themeSongTempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                        response.Content.CopyToAsync(fileStream).Wait();
                        fileStream.Close();
                        _logger.LogInformation("{seriesName} theme song downloaded in temp folder", serie.Name);

                        if (Plugin.Instance.Configuration.NormalizeAudio)
                        {
                            var normalizeAudio = new NormalizeAudio(_logger);
                            var normalizedFile = normalizeAudio.Process(themeSongTempFile).Result;
                            if (normalizedFile != null)
                            {
                                File.Move(normalizedFile, themeSongPath, true);
                                File.Delete(themeSongTempFile);
                            }
                            else
                            {
                                _logger.LogError("Failed to normalize audio for {seriesName}", serie.Name);
                                File.Delete(themeSongTempFile);
                                continue;
                            }
                        }
                        else
                        {
                            File.Move(themeSongTempFile, themeSongPath, true);
                        }

                        _logger.LogInformation("{seriesName} theme song succesfully downloaded", serie.Name);
                    }
                    catch (Exception e)
                    {
                        _logger.LogInformation("{seriesName} theme song not in database, or no internet connection {message} {exception}", serie.Name, e.Message, e.StackTrace);
                    }
                }
            }
        }



        private void OnTimerElapsed()
        {
            // Stop the timer until next update
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public Task RunAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
