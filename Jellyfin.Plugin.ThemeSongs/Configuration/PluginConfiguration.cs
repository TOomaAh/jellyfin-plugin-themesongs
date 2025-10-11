using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ThemeSongs.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public bool NormalizeAudio { get; set; } = true;
        public int NormalizeAudioVolume { get; set; } = -15;

        // Provider configuration
        public bool EnablePlexProvider { get; set; } = true;
        public bool EnableTelevisionTunesProvider { get; set; } = true;

        // Provider priorities (lower number = higher priority)
        public int PlexProviderPriority { get; set; } = 1;
        public int TelevisionTunesProviderPriority { get; set; } = 2;
    }
}
