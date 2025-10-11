using System.Threading.Tasks;

namespace Jellyfin.Plugin.ThemeSongs.Services
{
    public interface IAudioNormalizationService
    {
        Task<string> NormalizeAudioAsync(string inputFilePath);
        Task<bool> IsNormalizationRequiredAsync(string filePath);
    }
}