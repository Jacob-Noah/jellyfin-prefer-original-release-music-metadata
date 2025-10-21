using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.PreferOriginalReleaseMusicMetadata.Providers;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PreferOriginalReleaseMusicMetadata.LibraryMonitor;

/// <summary>
/// Monitors library changes and applies original release date metadata automatically.
/// </summary>
public sealed class LibraryChangeMonitor : IDisposable
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<LibraryChangeMonitor> _logger;
    private readonly OriginalReleaseDatePostScanTask _processor;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryChangeMonitor"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="logger">The logger.</param>
    public LibraryChangeMonitor(ILibraryManager libraryManager, ILogger<LibraryChangeMonitor> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
        _processor = new OriginalReleaseDatePostScanTask(
            logger as ILogger<OriginalReleaseDatePostScanTask> 
            ?? new LoggerFactory().CreateLogger<OriginalReleaseDatePostScanTask>());

        // Subscribe to library events
        _libraryManager.ItemAdded += OnItemAdded;
        _libraryManager.ItemUpdated += OnItemUpdated;
        
        _logger.LogInformation("Original Release Date library monitor started.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        _libraryManager.ItemUpdated -= OnItemUpdated;
        
        _logger.LogDebug("Original Release Date library monitor disposed.");
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        ProcessItemAsync(e).GetAwaiter().GetResult();
    }

    private void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        ProcessItemAsync(e).GetAwaiter().GetResult();
    }

    private async Task ProcessItemAsync(ItemChangeEventArgs e)
    {
        // Check if both the main feature and automatic processing are enabled
        if (Plugin.Instance?.Configuration.EnablePreferOriginalRelease != true)
        {
            return;
        }

        if (Plugin.Instance?.Configuration.EnableAutomaticProcessing != true)
        {
            _logger.LogDebug("Automatic processing is disabled, skipping item: {ItemName}", e.Item.Name);
            return;
        }

        // Only process music items
        if (e.Item is not MusicAlbum && e.Item is not Audio)
        {
            return;
        }

        try
        {
            if (_processor.ProcessItem(e.Item))
            {
                _logger.LogDebug(
                    "Automatically applied original release date to {ItemType}: {ItemName}",
                    e.Item.GetType().Name,
                    e.Item.Name);

                await _libraryManager.UpdateItemAsync(
                    e.Item,
                    e.Parent,
                    ItemUpdateType.MetadataEdit,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing original release date for {ItemType}: {ItemName}",
                e.Item.GetType().Name,
                e.Item.Name);
        }
    }
}
