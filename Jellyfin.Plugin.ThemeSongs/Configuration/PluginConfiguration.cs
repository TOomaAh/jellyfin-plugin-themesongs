using Jellyfin.Plugin.ThemeSongs.Provider;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ThemeSongs.Configuration
{


    public class PluginConfiguration : BasePluginConfiguration
    {

        public bool NormalizeAudio { get; set; } = true;
        public int NormalizeAudioVolume { get; set; } = -15;

    }

}
