using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.PreferOriginalReleaseMusicMetadata.Providers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PreferOriginalReleaseMusicMetadata.LibraryMonitor;

/// <summary>
/// Scheduled task to apply original release date metadata to music library.
/// </summary>
public class MusicLibraryPostScanTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<MusicLibraryPostScanTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicLibraryPostScanTask"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="logger">The logger.</param>
    public MusicLibraryPostScanTask(ILibraryManager libraryManager, ILogger<MusicLibraryPostScanTask> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Apply Original Release Date Metadata";

    /// <inheritdoc />
    public string Key => "PreferOriginalReleaseMusicMetadataTask";

    /// <inheritdoc />
    public string Description => "Applies original release date metadata to all music items in the library.";

    /// <inheritdoc />
    public string Category => "Library";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (Plugin.Instance?.Configuration.EnablePreferOriginalRelease != true)
        {
            _logger.LogInformation("Original release date preference is disabled, skipping task.");
            return;
        }

        _logger.LogInformation("Starting original release date metadata task.");

        var processor = new OriginalReleaseDatePostScanTask(_logger);
        var itemsProcessed = 0;
        var itemsUpdated = 0;

        // Get all music albums
        var albumsResult = _libraryManager.GetItemsResult(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.MusicAlbum },
            Recursive = true
        });

        // Get all audio items
        var audioItemsResult = _libraryManager.GetItemsResult(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Audio },
            Recursive = true
        });

        var allItems = albumsResult.Items.Concat(audioItemsResult.Items).ToList();
        var totalItems = allItems.Count;

        _logger.LogInformation("Found {Count} music items to process.", totalItems);
        _logger.LogInformation("Albums: {AlbumCount}, Audio items: {AudioCount}", 
            albumsResult.Items.Count, audioItemsResult.Items.Count);

        foreach (var item in allItems)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            _logger.LogDebug("Processing {ItemType}: {ItemName}", item.GetType().Name, item.Name);
            
            if (processor.ProcessItem(item))
            {
                try
                {
                    // Save the item to update the database
                    await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                    itemsUpdated++;
                    _logger.LogInformation("Updated {ItemName} with original release date", item.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating item {ItemName} in library", item.Name);
                }
            }

            itemsProcessed++;
            progress.Report((double)itemsProcessed / totalItems * 100);
        }

        _logger.LogInformation(
            "Original release date metadata task completed. Processed {Processed} items, updated {Updated} items.",
            itemsProcessed,
            itemsUpdated);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Users can trigger manually or set up their own schedule
        return Array.Empty<TaskTriggerInfo>();
    }
}
