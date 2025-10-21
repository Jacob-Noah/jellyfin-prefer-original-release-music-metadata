using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    private static readonly object CacheLock = new object();
    
    private const string CacheFileName = "processed-items-cache.json";

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

    private string GetCacheFilePath()
    {
        var pluginDataPath = Plugin.Instance?.DataFolderPath 
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "jellyfin", "plugins", "PreferOriginalReleaseMusicMetadata");
        
        Directory.CreateDirectory(pluginDataPath);
        return Path.Combine(pluginDataPath, CacheFileName);
    }

    private Dictionary<Guid, DateTime> LoadCache()
    {
        var cacheFilePath = GetCacheFilePath();
        
        lock (CacheLock)
        {
            try
            {
                if (File.Exists(cacheFilePath))
                {
                    var json = File.ReadAllText(cacheFilePath);
                    var cache = JsonSerializer.Deserialize<Dictionary<Guid, DateTime>>(json);
                    _logger.LogDebug("Loaded {Count} items from cache", cache?.Count ?? 0);
                    return cache ?? new Dictionary<Guid, DateTime>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load cache file, starting with empty cache");
            }

            return new Dictionary<Guid, DateTime>();
        }
    }

    private void SaveCache(Dictionary<Guid, DateTime> cache)
    {
        var cacheFilePath = GetCacheFilePath();
        
        lock (CacheLock)
        {
            try
            {
                var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(cacheFilePath, json);
                _logger.LogDebug("Saved {Count} items to cache", cache.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save cache file");
            }
        }
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (Plugin.Instance?.Configuration.EnablePreferOriginalRelease != true)
        {
            _logger.LogInformation("Original release date preference is disabled, skipping task.");
            return;
        }

        _logger.LogInformation("Starting original release date metadata task.");

        // Load the cache from disk
        var processedItems = LoadCache();

        var processor = new OriginalReleaseDatePostScanTask(_logger);
        var itemsProcessed = 0;
        var itemsUpdated = 0;
        var itemsSkipped = 0;
        var albumsUpdated = 0;

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

        var allAlbums = albumsResult.Items.Cast<MusicAlbum>().ToList();
        var allAudioItems = audioItemsResult.Items.Cast<Audio>().ToList();

        // Group audio items by parent album ID
        var tracksByAlbum = allAudioItems
            .Where(t => t.ParentId != Guid.Empty)
            .GroupBy(t => t.ParentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var totalItems = allAlbums.Count + allAudioItems.Count;

        _logger.LogInformation("Found {AlbumCount} albums and {TrackCount} tracks to process", 
            allAlbums.Count, allAudioItems.Count);

        // Process albums with their tracks in batches
        foreach (var album in allAlbums)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var albumTracksUpdated = new List<string>();
            var albumItemsProcessed = 0;
            var albumItemsSkipped = 0;

            // Process the album itself
            if (!ShouldSkipItem(album, processedItems, out var skipReason))
            {
                if (processor.ProcessItem(album))
                {
                    try
                    {
                        await album.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                        itemsUpdated++;
                        albumTracksUpdated.Add($"Album: {album.Name}");
                        // Cache if we updated it
                        processedItems[album.Id] = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating album {AlbumName}", album.Name);
                    }
                }
                else
                {
                    // Item was processed but no update needed - still cache it to avoid reprocessing
                    processedItems[album.Id] = DateTime.UtcNow;
                }
                albumItemsProcessed++;
            }
            else
            {
                albumItemsSkipped++;
            }

            // Process tracks in this album
            if (tracksByAlbum.TryGetValue(album.Id, out var tracks))
            {
                foreach (var track in tracks)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (!ShouldSkipItem(track, processedItems, out skipReason))
                    {
                        if (processor.ProcessItem(track))
                        {
                            try
                            {
                                await track.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                                itemsUpdated++;
                                albumTracksUpdated.Add(track.Name);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error updating track {TrackName}", track.Name);
                            }
                        }
                        // Cache after processing (whether updated or not)
                        processedItems[track.Id] = DateTime.UtcNow;
                        albumItemsProcessed++;
                    }
                    else
                    {
                        albumItemsSkipped++;
                    }
                }
            }

            // Log summary for this album if anything was updated
            if (albumTracksUpdated.Count > 0)
            {
                _logger.LogInformation(
                    "Updated album '{AlbumName}': {UpdateCount} item(s) with original release dates",
                    album.Name,
                    albumTracksUpdated.Count);
                albumsUpdated++;
            }

            itemsProcessed += albumItemsProcessed;
            itemsSkipped += albumItemsSkipped;
            progress.Report((double)itemsProcessed / totalItems * 100);
        }

        // Process orphaned tracks (tracks without an album)
        var orphanedTracks = allAudioItems.Where(t => t.ParentId == Guid.Empty || !tracksByAlbum.ContainsKey(t.ParentId)).ToList();
        if (orphanedTracks.Count > 0)
        {
            var orphanedUpdated = 0;
            foreach (var track in orphanedTracks)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (!ShouldSkipItem(track, processedItems, out var skipReason))
                {
                    if (processor.ProcessItem(track))
                    {
                        try
                        {
                            await track.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                            itemsUpdated++;
                            orphanedUpdated++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error updating orphaned track {TrackName}", track.Name);
                        }
                    }
                    // Cache after processing (whether updated or not)
                    processedItems[track.Id] = DateTime.UtcNow;
                    itemsProcessed++;
                }
                else
                {
                    itemsSkipped++;
                    itemsProcessed++;
                }
            }

            if (orphanedUpdated > 0)
            {
                _logger.LogInformation("Updated {Count} orphaned track(s) with original release dates", orphanedUpdated);
            }
        }

        // Save the updated cache to disk
        SaveCache(processedItems);

        _logger.LogInformation(
            "Completed: {AlbumsUpdated} albums processed, {TotalUpdated} total items updated, {Skipped} items skipped (no changes)",
            albumsUpdated,
            itemsUpdated,
            itemsSkipped);
    }

    private bool ShouldSkipItem(BaseItem item, Dictionary<Guid, DateTime> processedItems, out string reason)
    {
        if (processedItems.TryGetValue(item.Id, out var lastProcessedDate))
        {
            if (item.DateModified <= lastProcessedDate)
            {
                reason = "no changes since last processing";
                _logger.LogDebug("Skipping {ItemName} - {Reason}", item.Name, reason);
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Users can trigger manually or set up their own schedule
        return Array.Empty<TaskTriggerInfo>();
    }
}
