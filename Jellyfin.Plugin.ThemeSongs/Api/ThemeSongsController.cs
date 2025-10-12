using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ThemeSongs.Services;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.ThemeSongs.Api
{
    /// <summary>
    /// The Theme Songs API controller.
    /// </summary>
    [ApiController]
    [Route("ThemeSongs")]
    [Produces(MediaTypeNames.Application.Json)]
    public class ThemeSongsController : ControllerBase
    {
        private readonly IThemeSongDownloadService _downloadService;
        private readonly ILogger<ThemeSongsController> _logger;
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// Initializes a new instance of <see cref="ThemeSongsController"/>.
        /// </summary>
        /// <param name="downloadService">The theme song download service.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="libraryManager">The library manager.</param>
        public ThemeSongsController(
            IThemeSongDownloadService downloadService,
            ILogger<ThemeSongsController> logger,
            ILibraryManager libraryManager)
        {
            _downloadService = downloadService ?? throw new System.ArgumentNullException(nameof(downloadService));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _libraryManager = libraryManager ?? throw new System.ArgumentNullException(nameof(libraryManager));
        }

        /// <summary>
        /// Downloads all TV show theme songs.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        /// <response code="204">Theme song download started successfully.</response>
        /// <response code="500">Internal server error occurred.</response>
        [HttpPost("DownloadTVShows")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DownloadTVThemeSongsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting TV theme songs download via API");

            try
            {
                await _downloadService.DownloadAllThemeSongsAsync(cancellationToken);
                _logger.LogInformation("TV theme songs download completed successfully");
                return NoContent();
            }
            catch (System.OperationCanceledException)
            {
                _logger.LogInformation("TV theme songs download was cancelled");
                return NoContent();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error occurred during TV theme songs download");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while downloading theme songs");
            }
        }

        /// <summary>
        /// Downloads all TV show theme songs (synchronous version for backward compatibility).
        /// </summary>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        /// <response code="204">Theme song download started successfully.</response>
        /// <response code="500">Internal server error occurred.</response>
        [HttpPost("DownloadTVShowsSync")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult DownloadTVThemeSongsSync()
        {
            _logger.LogInformation("Starting TV theme songs download via API (sync)");

            try
            {
                _downloadService.DownloadAllThemeSongsAsync().GetAwaiter().GetResult();
                _logger.LogInformation("TV theme songs download completed successfully");
                return NoContent();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error occurred during TV theme songs download");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while downloading theme songs");
            }
        }

        /// <summary>
        /// Gets all TV series in the library.
        /// </summary>
        /// <returns>A list of TV series with their IDs and names.</returns>
        /// <response code="200">Returns the list of TV series.</response>
        [HttpGet("Series")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<SeriesDto>> GetAllSeries()
        {
            _logger.LogInformation("Getting all TV series");

            var series = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = [Jellyfin.Data.Enums.BaseItemKind.Series],
                Recursive = true
            })
            .OfType<Series>()
            .OrderBy(s => s.Name)
            .Select(s => new SeriesDto
            {
                Id = s.Id.ToString(),
                Name = s.Name,
                Path = s.Path
            })
            .ToList();

            return Ok(series);
        }

        /// <summary>
        /// Gets the theme song file for a specific series.
        /// </summary>
        /// <param name="seriesId">The series ID.</param>
        /// <returns>The theme song audio file.</returns>
        /// <response code="200">Returns the theme song file.</response>
        /// <response code="404">Theme song not found.</response>
        [HttpGet("Series/{seriesId}/Theme")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult GetThemeSong(string seriesId)
        {
            _logger.LogInformation("Getting theme song for series {SeriesId}", seriesId);

            var series = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = [Jellyfin.Data.Enums.BaseItemKind.Series],
            })
            .OfType<Series>()
            .FirstOrDefault(s => s.Id.ToString() == seriesId);

            if (series == null)
            {
                return NotFound("Series not found");
            }

            var themeSongPath = Path.Combine(series.Path, "theme.mp3");

            if (!System.IO.File.Exists(themeSongPath))
            {
                return NotFound("Theme song not found");
            }

            var fileStream = System.IO.File.OpenRead(themeSongPath);
            return File(fileStream, "audio/mpeg", "theme.mp3");
        }

        /// <summary>
        /// Uploads a theme song file for a specific series.
        /// </summary>
        /// <param name="seriesId">The series ID.</param>
        /// <param name="file">The theme song file to upload.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A result indicating success or failure.</returns>
        /// <response code="200">Theme song uploaded successfully.</response>
        /// <response code="400">Invalid file or series.</response>
        /// <response code="404">Series not found.</response>
        [HttpPost("Series/{seriesId}/Theme")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> UploadThemeSong(string seriesId, IFormFile file, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Uploading theme song for series {SeriesId}", seriesId);

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided");
            }

            // Validate file is audio
            var allowedContentTypes = new[] { "audio/mpeg", "audio/mp3", "audio/x-mpeg-3", "audio/x-mpeg" };
            if (!allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return BadRequest("File must be an MP3 audio file");
            }

            var series = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = [Data.Enums.BaseItemKind.Series],
            })
            .OfType<Series>()
            .FirstOrDefault(s => s.Id.ToString() == seriesId);

            if (series == null)
            {
                return NotFound("Series not found");
            }

            try
            {
                var themeSongPath = Path.Combine(series.Path, "theme.mp3");

                // Backup existing theme song if it exists
                if (System.IO.File.Exists(themeSongPath))
                {
                    var backupPath = Path.Combine(series.Path, $"theme.mp3.backup.{System.DateTime.Now:yyyyMMddHHmmss}");
                    System.IO.File.Move(themeSongPath, backupPath);
                    _logger.LogInformation("Backed up existing theme song to {BackupPath}", backupPath);
                }

                // Save the uploaded file
                using (var stream = new FileStream(themeSongPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                _logger.LogInformation("Theme song uploaded successfully for series {SeriesName}", series.Name);
                return Ok(new { message = "Theme song uploaded successfully" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error uploading theme song for series {SeriesId}", seriesId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while uploading the theme song");
            }
        }

        /// <summary>
        /// Deletes the theme song for a specific series.
        /// </summary>
        /// <param name="seriesId">The series ID.</param>
        /// <returns>A result indicating success or failure.</returns>
        /// <response code="200">Theme song deleted successfully.</response>
        /// <response code="404">Series or theme song not found.</response>
        [HttpDelete("Series/{seriesId}/Theme")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult DeleteThemeSong(string seriesId)
        {
            _logger.LogInformation("Deleting theme song for series {SeriesId}", seriesId);

            var series = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = [Data.Enums.BaseItemKind.Series],
            })
            .OfType<Series>()
            .FirstOrDefault(s => s.Id.ToString() == seriesId);

            if (series == null)
            {
                return NotFound("Series not found");
            }

            var themeSongPath = Path.Combine(series.Path, "theme.mp3");

            if (!System.IO.File.Exists(themeSongPath))
            {
                return NotFound("Theme song not found");
            }

            try
            {
                System.IO.File.Delete(themeSongPath);
                _logger.LogInformation("Theme song deleted successfully for series {SeriesName}", series.Name);
                return Ok(new { message = "Theme song deleted successfully" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error deleting theme song for series {SeriesId}", seriesId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the theme song");
            }
        }
    }

    /// <summary>
    /// DTO for series information.
    /// </summary>
    public class SeriesDto
    {
        /// <summary>
        /// Gets or sets the series ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the series name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the series path.
        /// </summary>
        public string Path { get; set; }
    }
}