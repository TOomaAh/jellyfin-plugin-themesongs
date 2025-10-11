using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeSongs.Services
{
    public class AudioNormalizationService : IAudioNormalizationService
    {
        private readonly ILogger<AudioNormalizationService> _logger;
        private const string FfmpegExecutable = "ffmpeg";

        public AudioNormalizationService(ILogger<AudioNormalizationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> NormalizeAudioAsync(string inputFilePath)
        {
            if (string.IsNullOrEmpty(inputFilePath))
            {
                throw new ArgumentException("Input file path cannot be null or empty", nameof(inputFilePath));
            }

            ValidateInputFile(inputFilePath);

            try
            {
                var config = Plugin.Instance?.Configuration;
                if (config?.NormalizeAudio != true)
                {
                    _logger.LogDebug("Audio normalization is disabled in configuration");
                    return inputFilePath;
                }

                string targetVolume = $"{config.NormalizeAudioVolume}dB";
                string currentVolume = await DetectVolumeAsync(inputFilePath);

                if (IsVolumeAlreadyNormalized(targetVolume, currentVolume))
                {
                    _logger.LogInformation("Audio volume is already normalized for {FilePath}: {CurrentVolume}",
                        inputFilePath, currentVolume);
                    return inputFilePath;
                }

                return await NormalizeAudioFileAsync(inputFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to normalize audio for {FilePath}", inputFilePath);
                throw;
            }
        }

        public async Task<bool> IsNormalizationRequiredAsync(string filePath)
        {
            try
            {
                var config = Plugin.Instance?.Configuration;
                if (config?.NormalizeAudio != true)
                {
                    return false;
                }

                string targetVolume = $"{config.NormalizeAudioVolume}dB";
                string currentVolume = await DetectVolumeAsync(filePath);

                return !IsVolumeAlreadyNormalized(targetVolume, currentVolume);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine if normalization is required for {FilePath}", filePath);
                return false;
            }
        }

        private async Task<string> DetectVolumeAsync(string filePath)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = FfmpegExecutable,
                Arguments = $"-i \"{filePath}\" -af \"volumedetect\" -f null -",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };

            _logger.LogDebug("Running volume detection for {FilePath}", filePath);
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"FFmpeg volume detection failed: {error}");
            }

            return ExtractVolumeFromOutput(error);
        }

        private async Task<string> NormalizeAudioFileAsync(string inputFilePath)
        {
            string outputPath = GenerateOutputPath(inputFilePath);

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            string arguments = $"-i \"{inputFilePath}\" -af \"volume=-1dB, loudnorm\" -y \"{outputPath}\"";

            var processInfo = new ProcessStartInfo
            {
                FileName = FfmpegExecutable,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };

            _logger.LogInformation("Normalizing audio file {InputPath} to {OutputPath}", inputFilePath, outputPath);
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError("FFmpeg normalization failed with error: {Error}", error);
                throw new InvalidOperationException($"FFmpeg normalization failed: {error}");
            }

            _logger.LogDebug("Audio normalization completed successfully for {FilePath}", inputFilePath);
            return outputPath;
        }

        private static string ExtractVolumeFromOutput(string ffmpegOutput)
        {
            string[] lines = ffmpegOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                if (line.Contains("max_volume"))
                {
                    int colonIndex = line.LastIndexOf(':');
                    if (colonIndex > 0 && colonIndex < line.Length - 1)
                    {
                        return line.Substring(colonIndex + 1).Trim();
                    }

                    string[] parts = line.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        return $"{parts[^2]} {parts[^1]}";
                    }
                }
            }

            throw new InvalidOperationException("Could not extract volume information from FFmpeg output");
        }

        private bool IsVolumeAlreadyNormalized(string targetVolume, string currentVolume)
        {
            try
            {
                double target = ParseVolumeValue(targetVolume);
                double current = ParseVolumeValue(currentVolume);
                const double epsilon = 0.5;

                return Math.Abs(target - current) < epsilon;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not compare volumes: target={TargetVolume}, current={CurrentVolume}",
                    targetVolume, currentVolume);
                return false;
            }
        }

        private static double ParseVolumeValue(string volumeString)
        {
            if (string.IsNullOrEmpty(volumeString))
            {
                throw new ArgumentException("Volume string is null or empty");
            }

            var match = Regex.Match(volumeString, @"volume:\s*([-\d.]+)\s*dB");
            if (match.Success)
            {
                volumeString = match.Groups[1].Value;
            }
            else if (volumeString.EndsWith("dB", StringComparison.OrdinalIgnoreCase))
            {
                int colonIndex = volumeString.LastIndexOf(':');
                if (colonIndex > 0)
                {
                    volumeString = volumeString.Substring(colonIndex + 1);
                }
                volumeString = volumeString.Replace("dB", "").Trim();
            }

            if (!double.TryParse(volumeString, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            {
                throw new FormatException($"Invalid volume format: {volumeString}");
            }

            return result;
        }

        private static string GenerateOutputPath(string inputFilePath)
        {
            string directory = Path.GetDirectoryName(inputFilePath);
            string fileName = Path.GetFileNameWithoutExtension(inputFilePath);
            string extension = Path.GetExtension(inputFilePath);

            return Path.Combine(directory, $"normalized_{fileName}{extension}");
        }

        private static void ValidateInputFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            try
            {
                using var fileStream = File.OpenRead(filePath);
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException($"File is not accessible: {filePath}", ex);
            }
        }
    }
}