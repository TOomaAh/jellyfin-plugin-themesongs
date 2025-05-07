using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeSongs
{
    class NormalizeAudio
    {

        private readonly ILogger<ThemeSongsManager> _logger;

        public NormalizeAudio(ILogger<ThemeSongsManager> logger)
        {
            _logger = logger;
        }


        public async Task<string> Process(string filePath)
        {

            CheckInputIsReadable(filePath);

            string volumeWanted = $"{Plugin.Instance?.Configuration.NormalizeAudioVolume ?? -10}dB";
            string volumeDetected = VolumeDetector(_logger, filePath);

            if (CompareVolumes(_logger, volumeWanted, volumeDetected) < 1)
            {
                _logger.LogInformation("Volume is already normalized: {volumeDetected}", volumeDetected);
                return filePath;
            }

            string ffmpegPath = "ffmpeg";
            string outputPath = Path.Combine(Path.GetDirectoryName(filePath), "normalized_" + Path.GetFileName(filePath));
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
            // reduce volume to -1dB and normalize audio
            string arguments = $"-i \"{filePath}\" -af \"volume=-1dB, loudnorm\" -y \"{outputPath}\"";
            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo.FileName = ffmpegPath;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                _logger.LogInformation("Running ffmpeg with arguments: {arguments}", arguments);
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    _logger.LogError("ffmpeg failed with error: {error}", error);
                    return null;
                }

                _logger.LogInformation("ffmpeg output: {output}", output);
            }
            return outputPath;
        }


        private static string VolumeDetector(ILogger<ThemeSongsManager> logger, string filePath)
        {
            string ffmpegPath = "ffmpeg";
            string arguments = $"-i \"{filePath}\" -af \"volumedetect\" -f null -";
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = ffmpegPath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"ffmpeg failed with error: {error}");
            }

            // ffmpeg log all the information in the stderr
            return GetVolume(logger, error);
        }

        private static string GetVolume(ILogger<ThemeSongsManager> logger, string output)
        {
            string[] lines = output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                if (line.Contains("max_volume"))
                {
                    int colonIndex = line.LastIndexOf(':');
                    if (colonIndex > 0 && colonIndex < line.Length - 1)
                    {
                        string value = line.Substring(colonIndex + 1).Trim();
                        return value;
                    }

                    string[] parts = line.Split([" "], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        string value = $"{parts[^2]} {parts[^1]}";
                        return value; // Retourne "0.0 dB" au lieu de juste "dB"
                    }
                }
            }
            return null;
        }

        
        private static double ParseVolumeValue(ILogger<ThemeSongsManager> logger, string volumeStr)
        {
            if (string.IsNullOrEmpty(volumeStr))
            {
                throw new ArgumentException("Volume string is null or empty");
            }
            var match = System.Text.RegularExpressions.Regex.Match(volumeStr, @"volume:\s*([-\d.]+)\s*dB");
            if (match.Success)
            {
                volumeStr = match.Groups[1].Value;
            }
            else if (volumeStr.EndsWith("dB", StringComparison.OrdinalIgnoreCase))
            {
                int colonIndex = volumeStr.LastIndexOf(':');
                if (colonIndex > 0)
                {
                    volumeStr = volumeStr.Substring(colonIndex + 1);
                }
                volumeStr = volumeStr.Replace("dB", "").Trim();
            }

            // Essayer de parser en double
            if (!double.TryParse(volumeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            {
                throw new FormatException($"Invalid volume format: {volumeStr}");
            }
            return result;
        }

        // Compare the volume with the wanted volume
        // return -1 if the volume is lower than the wanted volume
        // return 0 if the volume is equal to the wanted volume
        // return 1 if the volume is higher than the wanted volume
        // example of volume: -1.0dB
        public static int CompareVolumes(ILogger<ThemeSongsManager> logger, string volumeWanted, string currentVolume)
        {

            // string to double
            double wanted = ParseVolumeValue(logger, volumeWanted);
            double current = ParseVolumeValue(logger, currentVolume);
            const double epsilon = 0.5;

            if (Math.Abs(wanted - current) < epsilon)
                return 0;
            return wanted < current ? -1 : 1;
        }


        private static void CheckInputIsReadable(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            try
            {
                // Try to open the file to check if we have access
                using var fileStream = File.OpenRead(filePath);
                // File is accessible, close stream immediately
                fileStream.Close();
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException($"File is not accessible: {filePath}", ex);
            }

        }
    }
}