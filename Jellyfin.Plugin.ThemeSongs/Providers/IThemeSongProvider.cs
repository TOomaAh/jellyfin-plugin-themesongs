using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.ThemeSongs.Providers
{
    public interface IThemeSongProvider
    {
        string Name { get; }
        int Priority { get; }
        Task<string> GetThemeSongUrlAsync(Series series, CancellationToken cancellationToken = default);
    }
}