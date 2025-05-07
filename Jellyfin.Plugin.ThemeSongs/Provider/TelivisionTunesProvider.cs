using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Collections.Generic;

namespace Jellyfin.Plugin.ThemeSongs.Provider
{
    public class TelevisionTunesProvider(ILogger<TelevisionTunesProvider> logger) : IProvider
    {
        private readonly ILogger<TelevisionTunesProvider> _logger = logger;
        private static readonly CultureInfo UsCulture = new("en-US");
        private static readonly string _baseUrl = "http://televisiontunes.com";

        public async Task<string> GetURL(Series item, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Recovering URL for {name}", item.Name);
            // Extract the first letter of the title to determine the section
            string section = GetSearchTitle(item.Name).Substring(0, 1);
            if (int.TryParse(section, NumberStyles.Integer, UsCulture, out _))
            {
                section = "numbers";
            }

            // Construit l'URL pour la section
            string url = $"${_baseUrl}/{section}-theme-songs.html";
            
            // Récupère le contenu HTML de la page
            string html = await GetHtmlContent(url, cancellationToken);
            if (html == null)
            {
                _logger.LogWarning("Échec de récupération du HTML pour {0}", url);
                return null;
            }

            // ecrire le contenu HTML dans un fichier pour le débogage
            string filePath = Path.Combine(Path.GetTempPath(), "televisiontunes.html");
            File.WriteAllText(filePath, html);

            // Recherche correspondante pour trouver l'URL de la série
            Match match = FindSeriesMatch(html, item.Name);
            if (match.Success)
            {
                string seriesUrl = $"{_baseUrl}/" + match.Groups["url"].Value;
                string themeUrl = await GetThemeSongFromPage(seriesUrl, cancellationToken);
                return themeUrl;
            }

            return null;
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

        private async Task<string> GetThemeSongFromPage(string url, CancellationToken cancellationToken)
        {
            string html = await GetHtmlContent(url, cancellationToken);
            if (html == null)
                return null;

            Match matchCollection = Regex.Match(html, "televisiontunes.com/uploads/audio/(?<themesong>.*?).mp3", RegexOptions.IgnoreCase);
            if (!matchCollection.Success)
                return null;

            string themeUrl = $"{_baseUrl}/uploads/audio/" +
                WebUtility.HtmlDecode(matchCollection.Groups["themesong"].Value) + ".mp3";

            _logger.LogInformation("Theme Song Found: {0}", themeUrl);
            return themeUrl;
        }

        private async Task<string> GetHtmlContent(string url, CancellationToken cancellationToken)
        {
            try
            {
                HttpClientHandler handler = new()
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                };

                using HttpClient client = new(handler);
                client.DefaultRequestHeaders.Add("User-Agent", "Jellyfin/10.0");
                HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Cannot get html content {ex}", ex);
            }

            return null;
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