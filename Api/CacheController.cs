using System;
using System.IO;
using MediaBrowser.Common.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PreferOriginalReleaseMusicMetadata.Api
{
    /// <summary>
    /// API controller for cache management operations.
    /// </summary>
    [ApiController]
    [Authorize(Policy = "RequiresElevation")]
    [Route("PreferOriginalReleaseMusicMetadata")]
    public class CacheController : ControllerBase
    {
        private readonly IApplicationPaths _applicationPaths;
        private readonly ILogger<CacheController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheController"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{CacheController}"/> interface.</param>
        public CacheController(IApplicationPaths applicationPaths, ILogger<CacheController> logger)
        {
            _applicationPaths = applicationPaths;
            _logger = logger;
        }

        /// <summary>
        /// Deletes the processing cache file.
        /// </summary>
        /// <returns>OK if successful.</returns>
        [HttpDelete("Cache")]
        public ActionResult ClearCache()
        {
            try
            {
                var pluginDataPath = Path.Combine(_applicationPaths.PluginsPath, "Jellyfin.Plugin.PreferOriginalReleaseMusicMetadata");
                var cacheFilePath = Path.Combine(pluginDataPath, "processed-items-cache.json");

                if (System.IO.File.Exists(cacheFilePath))
                {
                    System.IO.File.Delete(cacheFilePath);
                    _logger.LogInformation("Processing cache cleared successfully at: {CacheFilePath}", cacheFilePath);
                    return Ok(new { message = "Cache cleared successfully" });
                }
                else
                {
                    _logger.LogInformation("Cache file does not exist at: {CacheFilePath}", cacheFilePath);
                    return Ok(new { message = "Cache file does not exist" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
                return StatusCode(500, new { message = $"Error clearing cache: {ex.Message}" });
            }
        }
    }
}
