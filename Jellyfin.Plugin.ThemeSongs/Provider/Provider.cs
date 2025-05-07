using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Search;

namespace Jellyfin.Plugin.ThemeSongs.Provider
{
    public interface IProvider
    {
        public Task<string> GetURL(Series item, CancellationToken cancellationToken);
    }
}