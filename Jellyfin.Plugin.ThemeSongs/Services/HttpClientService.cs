using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeSongs.Services
{
    public class HttpClientService : IHttpClientService, IDisposable
    {
        private const int MaxRetries = 3;
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpClientService> _logger;

        public HttpClientService(ILogger<HttpClientService> logger)
        {
            _logger = logger;
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };

            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Jellyfin-ThemeSongs/1.0");
        }

        public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Making GET request to {Url}", url);
                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync(cancellationToken);
                }

                _logger.LogWarning("HTTP request failed with status {StatusCode} for URL {Url}",
                    response.StatusCode, url);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making GET request to {Url}", url);
                return null;
            }
        }

        public async Task<bool> HeadRequestAsync(string url, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Making HEAD request to {Url}", url);
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                var response = await _httpClient.SendAsync(request, cancellationToken);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making HEAD request to {Url}", url);
                return false;
            }
        }

        public async Task<bool> DownloadFileAsync(string url, string filePath, CancellationToken cancellationToken = default)
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Downloading file from {Url} to {FilePath}", url, filePath);

                    var response = await _httpClient.GetAsync(url, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        bool isRetryable = (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests;
                        if (isRetryable && attempt < MaxRetries)
                        {
                            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                            _logger.LogWarning("Download failed with {StatusCode} for {Url}, retrying in {Delay}s (attempt {Attempt}/{Max})",
                                response.StatusCode, url, delay.TotalSeconds, attempt, MaxRetries);
                            await Task.Delay(delay, cancellationToken);
                            continue;
                        }

                        _logger.LogWarning("Download failed with status {StatusCode} for {Url}", response.StatusCode, url);
                        return false;
                    }

                    var directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fileStream, cancellationToken);

                    _logger.LogInformation("Successfully downloaded file to {FilePath}", filePath);
                    return true;
                }
                catch (HttpRequestException ex) when (attempt < MaxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    _logger.LogWarning(ex, "Network error downloading {Url}, retrying in {Delay}s (attempt {Attempt}/{Max})",
                        url, delay.TotalSeconds, attempt, MaxRetries);
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error downloading file from {Url} to {FilePath}", url, filePath);
                    return false;
                }
            }

            return false;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}