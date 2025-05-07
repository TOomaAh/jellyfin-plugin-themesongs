using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ThemeSongs.Provider;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeSongs.ScheduledTasks
{
    public class DownloadThemeSongsTask : IScheduledTask
    {
        private readonly ILogger<ThemeSongsManager> _logger;
        private readonly ThemeSongsManager _themeSongsManager;

        public DownloadThemeSongsTask(ILibraryManager libraryManager, ILogger<ThemeSongsManager> logger, IServerApplicationPaths serverApplicationPaths, PlexProvider plexProvider, TelevisionTunesProvider televisionTunesProvider)
        {
            _logger = logger;
            _themeSongsManager = new ThemeSongsManager(libraryManager,  logger, serverApplicationPaths, plexProvider, televisionTunesProvider);
        }
        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.LogInformation("Starting plugin, Downloading TV Theme Songs...");
            _themeSongsManager.DownloadAllThemeSongs();
            _logger.LogInformation("All theme songs downloaded");
            return Task.CompletedTask;
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
