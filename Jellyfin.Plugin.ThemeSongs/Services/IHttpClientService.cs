using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.ThemeSongs.Services
{
    public interface IHttpClientService
    {
        Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default);
        Task<bool> HeadRequestAsync(string url, CancellationToken cancellationToken = default);
        Task<bool> DownloadFileAsync(string url, string filePath, CancellationToken cancellationToken = default);
    }
}