using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ThemeSongs.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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

        /// <summary>
        /// Initializes a new instance of <see cref="ThemeSongsController"/>.
        /// </summary>
        /// <param name="downloadService">The theme song download service.</param>
        /// <param name="logger">The logger.</param>
        public ThemeSongsController(
            IThemeSongDownloadService downloadService,
            ILogger<ThemeSongsController> logger)
        {
            _downloadService = downloadService ?? throw new System.ArgumentNullException(nameof(downloadService));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
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
    }
}