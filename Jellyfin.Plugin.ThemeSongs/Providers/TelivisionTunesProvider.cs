using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.ThemeSongs.Services;

namespace Jellyfin.Plugin.ThemeSongs.Providers
{
    public class TelevisionTunesProvider : IThemeSongProvider
    {
        private readonly IHttpClientService _httpClientService;
        private readonly ILogger<TelevisionTunesProvider> _logger;
        private static readonly CultureInfo UsCulture = new("en-US");
        private const string BaseUrl = "http://televisiontunes.com";

        public TelevisionTunesProvider(IHttpClientService httpClientService, ILogger<TelevisionTunesProvider> logger)
        {
            _httpClientService = httpClientService ?? throw new ArgumentNullException(nameof(httpClientService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Name => "TelevisionTunes";
        public int Priority => Plugin.Instance?.Configuration?.TelevisionTunesProviderPriority ?? 2;

        public async Task<string> GetThemeSongUrlAsync(Series series, CancellationToken cancellationToken = default)
        {
            if (series == null)
            {
                throw new ArgumentNullException(nameof(series));
            }

            _logger.LogDebug("Searching for theme song for {SeriesName} on TelevisionTunes", series.Name);

            try
            {
                // Extract the first letter of the title to determine the section
                string section = GetSearchTitle(series.Name).Substring(0, 1);
                if (int.TryParse(section, NumberStyles.Integer, UsCulture, out _))
                {
                    section = "numbers";
                }

                // Build the URL for the section
                string url = $"{BaseUrl}/{section}-theme-songs.html";

                // Get HTML content from the page
                string html = await _httpClientService.GetStringAsync(url, cancellationToken);
                if (string.IsNullOrEmpty(html))
                {
                    _logger.LogWarning("Failed to retrieve HTML content for {Url}", url);
                    return null;
                }

                // Find matching series in the HTML
                Match match = FindSeriesMatch(html, series.Name);
                if (match.Success)
                {
                    string seriesUrl = $"{BaseUrl}/" + match.Groups["url"].Value;
                    string themeUrl = await GetThemeSongFromPageAsync(seriesUrl, cancellationToken);

                    if (!string.IsNullOrEmpty(themeUrl))
                    {
                        _logger.LogInformation("Found theme song for {SeriesName} on TelevisionTunes", series.Name);
                        return themeUrl;
                    }
                }

                _logger.LogDebug("No theme song found for {SeriesName} on TelevisionTunes", series.Name);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for theme song for {SeriesName} on TelevisionTunes", series.Name);
                return null;
            }
        }

        private Match FindSeriesMatch(string html, string seriesName)
        {
            // Liste de variants du nom à essayer
            List<string> nameVariants =
            [
                seriesName,
            ];

            // Reolace "&" par "and"
            string replacedName = Regex.Replace(seriesName, "\\&", "and");
            if (replacedName != seriesName)
                nameVariants.Add(replacedName);

            // Without '()'
            replacedName = Regex.Replace(seriesName, ".\\(.*?\\)", "").Trim();
            if (replacedName != seriesName)
                nameVariants.Add(replacedName);

            // Without special characters
            replacedName = Regex.Replace(seriesName, "\\.|\\/|\\'", "").Trim();
            if (replacedName != seriesName && !nameVariants.Contains(replacedName))
                nameVariants.Add(replacedName);

            // Replace special characters with spaces
            replacedName = Regex.Replace(seriesName, "\\.|\\/|\\'", " ").Trim();
            if (replacedName != seriesName && !nameVariants.Contains(replacedName))
                nameVariants.Add(replacedName);

            // Just the first part of the title (before " - ")
            if (seriesName.Contains(" - "))
            {
                string[] parts = seriesName.Split(new string[] { " - " }, StringSplitOptions.None);
                replacedName = parts[0].Trim();
                if (replacedName != seriesName && !nameVariants.Contains(replacedName))
                    nameVariants.Add(replacedName);
            }

            // Search for the URL in the HTML content
            string[] patterns =
            [
                "<li><a href=\"/(?<url>.*?)\"\\s*>\\s*{0}\\s*</a></li>",  // With flexible spaces
                "<li><a href=\"/(?<url>.*?)\"\\s*>\\s*{0}\\s*-",           // With flexible spaces and a dash
                "<li><a href=\"/(?<url>.*?)\"\\s*>\\s*{0}</a></li>",       // Without space
                "<li><a href=\"/(?<url>.*?)\"\\s*>\\s*{0}\\s*- "            // Without space and a dash after
            ];

            // Try to find a match for each variant
            foreach (string variant in nameVariants)
            {
                foreach (string pattern in patterns)
                {
                    string escapedName = Escape(variant);
                    string regex = string.Format(pattern, escapedName);
                    Match match = Regex.Match(html, regex, RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

                    if (match.Success)
                    {
                        _logger.LogInformation("Match with pattern {pattern} with {variant}", pattern, variant);
                        return match;
                    }
                }
            }
            return Match.Empty;
        }

        private async Task<string> GetThemeSongFromPageAsync(string url, CancellationToken cancellationToken)
        {
            string html = await _httpClientService.GetStringAsync(url, cancellationToken);
            if (string.IsNullOrEmpty(html))
                return null;

            Match matchCollection = Regex.Match(html, "televisiontunes.com/uploads/audio/(?<themesong>.*?).mp3", RegexOptions.IgnoreCase);
            if (!matchCollection.Success)
                return null;

            string themeUrl = $"{BaseUrl}/uploads/audio/" +
                WebUtility.HtmlDecode(matchCollection.Groups["themesong"].Value) + ".mp3";

            _logger.LogDebug("Theme Song Found: {ThemeUrl}", themeUrl);
            return themeUrl;
        }

        private static string GetSearchTitle(string name)
        {
            string[] articles = ["The", "A"];

            foreach (string article in articles)
            {
                if (name.StartsWith(article + " ", StringComparison.OrdinalIgnoreCase))
                {
                    return name[(article.Length + 1)..].Trim() + ", " + article;
                }
            }

            return name;
        }

        private static string Escape(string text)
        {
            char[] specialChars = ['[', '\\', '^', '$', '.', '|', '?', '*', '+', '(', ')'];
            StringBuilder sb = new();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (Array.IndexOf(specialChars, c) != -1)
                {
                    sb.Append('\\');
                }
                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}