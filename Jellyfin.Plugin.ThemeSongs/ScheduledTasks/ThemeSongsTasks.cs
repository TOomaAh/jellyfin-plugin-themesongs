using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ThemeSongs.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeSongs.ScheduledTasks
{
    public class DownloadThemeSongsTask : IScheduledTask
    {
        private readonly IThemeSongDownloadService _downloadService;
        private readonly ILogger<DownloadThemeSongsTask> _logger;

        public DownloadThemeSongsTask(IThemeSongDownloadService downloadService, ILogger<DownloadThemeSongsTask> logger)
        {
            _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.LogInformation("Starting scheduled theme songs download task");

            try
            {
                await _downloadService.DownloadAllThemeSongsAsync(cancellationToken);
                _logger.LogInformation("Scheduled theme songs download task completed successfully");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Scheduled theme songs download task was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during scheduled theme songs download task");
                throw;
            }
        }

        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            return Execute(cancellationToken, progress);
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Run this task every 24 hours
            yield return new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerInterval, 
                IntervalTicks = TimeSpan.FromHours(24).Ticks
            };
        }

        public string Name => "Download TV Theme Songs";
        public string Key => "DownloadTV ThemeSongs";
        public string Description => "Scans all libraries to download TV Theme Songs";
        public string Category => "Theme Songs";
    }
}
