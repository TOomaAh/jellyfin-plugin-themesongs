using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.ThemeSongs.Services;

namespace Jellyfin.Plugin.ThemeSongs
{
    public class ThemeSongsManager : IDisposable
    {
        private readonly IThemeSongDownloadService _downloadService;
        private readonly ILogger<ThemeSongsManager> _logger;
        private readonly Timer _timer;

        public ThemeSongsManager(IThemeSongDownloadService downloadService, ILogger<ThemeSongsManager> logger)
        {
            _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task DownloadAllThemeSongsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting theme song download process");

            try
            {
                await _downloadService.DownloadAllThemeSongsAsync(cancellationToken);
                _logger.LogInformation("Theme song download process completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during theme song download process");
                throw;
            }
        }

        public void DownloadAllThemeSongs()
        {
            // Synchronous wrapper for backward compatibility
            try
            {
                DownloadAllThemeSongsAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in synchronous theme song download");
                throw;
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
            _timer?.Dispose();
        }
    }
}
