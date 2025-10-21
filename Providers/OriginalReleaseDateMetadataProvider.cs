using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PreferOriginalReleaseMusicMetadata.Providers;

/// <summary>
/// External ID provider that captures original release date metadata.
/// </summary>
public class OriginalReleaseDateExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => "Original Release Date";

    /// <inheritdoc />
    public string Key => "OriginalReleaseDate";

    /// <inheritdoc />
    public ExternalIdMediaType? Type => null;

    /// <inheritdoc />
    public string UrlFormatString => string.Empty;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item)
    {
        return item is MusicAlbum || item is Audio;
    }
}

/// <summary>
/// Metadata post-processor that applies original release date metadata.
/// </summary>
public class OriginalReleaseDatePostScanTask
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OriginalReleaseDatePostScanTask"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public OriginalReleaseDatePostScanTask(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Processes a music item to apply original release date metadata.
    /// </summary>
    /// <param name="item">The item to process.</param>
    /// <returns>True if the item was modified.</returns>
    public bool ProcessItem(BaseItem item)
    {
        if (Plugin.Instance?.Configuration.EnablePreferOriginalRelease != true)
        {
            return false;
        }

        if (item is not MusicAlbum && item is not Audio)
        {
            return false;
        }

        DateTime? originalDate = ExtractOriginalReleaseDate(item);

        if (originalDate.HasValue && originalDate != item.PremiereDate)
        {
            _logger.LogInformation(
                "Updating {ItemType} '{ItemName}' premiere date from {OldDate} to original release date {NewDate}",
                item.GetType().Name,
                item.Name,
                item.PremiereDate?.ToString("yyyy-MM-dd") ?? "null",
                originalDate.Value.ToString("yyyy-MM-dd"));

            item.PremiereDate = originalDate;
            item.ProductionYear = originalDate.Value.Year;
            return true;
        }

        return false;
    }

    private DateTime? ExtractOriginalReleaseDateFromAlbum(MusicAlbum album)
    {
        // Reads the first audio file in the album directory to extract original year metadata
        // All tracks in an album should contain the same original release year
        DateTime? oldestDate = null;
        int trackCount = 0;
        
        // Find audio files in the album directory to extract metadata
        if (!string.IsNullOrEmpty(album.Path) && System.IO.Directory.Exists(album.Path))
        {
            var audioFiles = System.IO.Directory.GetFiles(album.Path, "*.*", System.IO.SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                .Take(1); // Just check the first audio file
            
            foreach (var audioFile in audioFiles)
            {
                trackCount++;
                try
                {
                    using var file = TagLib.File.Create(audioFile);
                    var id3v2Tag = file.GetTag(TagLib.TagTypes.Id3v2) as TagLib.Id3v2.Tag;
                    
                    if (id3v2Tag != null)
                    {
                        // Check for TXXX originalyear tag
                        var userTextFrames = id3v2Tag.GetFrames<TagLib.Id3v2.UserTextInformationFrame>();
                        foreach (var userFrame in userTextFrames)
                        {
                            var description = userFrame.Description?.ToLowerInvariant() ?? "";
                            if (description.Contains("original") && 
                                (description.Contains("year") || description.Contains("date")) &&
                                userFrame.Text.Length > 0)
                            {
                                if (int.TryParse(userFrame.Text[0], out var yearValue) && 
                                    yearValue > 1800 && yearValue <= DateTime.Now.Year + 5)
                                {
                                    oldestDate = new DateTime(yearValue, 1, 1);
                                    _logger.LogDebug("Found original year {Year} in track file: {File}", yearValue, audioFile);
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (oldestDate.HasValue)
                        break; // Found a date, no need to check more files
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error reading track file {File}", audioFile);
                }
            }
        }
        
        if (oldestDate.HasValue)
        {
            _logger.LogInformation("Found original release date from album tracks for {AlbumName}: {Date} (checked {TrackCount} files)", 
                album.Name, oldestDate.Value.ToString("yyyy-MM-dd"), trackCount);
        }
        else
        {
            _logger.LogDebug("No original release date found in tracks for album {AlbumName} (checked {TrackCount} files in {Path})", 
                album.Name, trackCount, album.Path ?? "null");
        }
        
        return oldestDate;
    }

    private DateTime? ExtractOriginalReleaseDate(BaseItem item)
    {
        _logger.LogDebug("Extracting original release date for {ItemType} '{ItemName}' (Path: {Path}, Current PremiereDate: {PremiereDate})", 
            item.GetType().Name, item.Name, item.Path ?? "null", item.PremiereDate?.ToString("yyyy-MM-dd") ?? "null");

        // For albums, check the tracks to find the oldest original release date
        if (item is MusicAlbum album)
        {
            return ExtractOriginalReleaseDateFromAlbum(album);
        }

        return ExtractOriginalReleaseDateFromFile(item);
    }

    private DateTime? ExtractOriginalReleaseDateFromFile(BaseItem item)
    {
        // First check ProviderIds (for manually set values)
        if (item.ProviderIds != null)
        {
            // Check for TDOR (ID3v2.4 Original Release Year) tag
            if (item.ProviderIds.TryGetValue("TDOR", out var tdorValue) &&
                DateTime.TryParse(tdorValue, out var tdorDate))
            {
                _logger.LogInformation("Found TDOR tag in ProviderIds for {ItemName}: {Date}", item.Name, tdorDate);
                return tdorDate;
            }

            // Check for explicit OriginalReleaseDate
            if (item.ProviderIds.TryGetValue("OriginalReleaseDate", out var dateValue) &&
                DateTime.TryParse(dateValue, out var parsedDate))
            {
                _logger.LogInformation("Found OriginalReleaseDate in ProviderIds for {ItemName}: {Date}", item.Name, parsedDate);
                return parsedDate;
            }

            // Check for OriginalYear
            if (item.ProviderIds.TryGetValue("OriginalYear", out var yearValue) &&
                int.TryParse(yearValue, out var year) &&
                year > 1800 && year <= DateTime.Now.Year + 5)
            {
                _logger.LogInformation("Found OriginalYear in ProviderIds for {ItemName}: {Year}", item.Name, year);
                return new DateTime(year, 1, 1);
            }
        }

        // Read metadata from file tags for audio items with a file path
        if (!string.IsNullOrEmpty(item.Path))
        {
            if (!System.IO.File.Exists(item.Path))
            {
                _logger.LogDebug("File path exists but file not found: {Path}", item.Path);
                return null;
            }

            try
            {
                _logger.LogInformation("Attempting to read tags from file: {Path}", item.Path);
                using var file = TagLib.File.Create(item.Path);

                // Check for Vorbis comments (FLAC, OGG) or APE tags first - they use ORIGINALDATE
                var xiphTag = file.GetTag(TagLib.TagTypes.Xiph) as TagLib.Ogg.XiphComment;
                if (xiphTag != null)
                {
                    _logger.LogInformation("Found Xiph/Vorbis tag in file: {Path}", item.Path);

                    // Log all available fields for debugging
                    var fieldCount = xiphTag.FieldCount;
                    _logger.LogInformation("Vorbis comment has {Count} fields", fieldCount);

                    // Check for ORIGINALDATE field (MusicBrainz standard) - try various casings
                    var originalDate = xiphTag.GetFirstField("ORIGINALDATE") 
                                     ?? xiphTag.GetFirstField("ORIGINAL DATE")
                                     ?? xiphTag.GetFirstField("originaldate")
                                     ?? xiphTag.GetFirstField("original date");
                    if (!string.IsNullOrEmpty(originalDate))
                    {
                        _logger.LogInformation("Found ORIGINALDATE field with value: {Value}", originalDate);
                        if (DateTime.TryParse(originalDate, out var parsedDate))
                        {
                            _logger.LogInformation("Found ORIGINALDATE in file for {ItemName}: {Date}", item.Name, parsedDate);
                            return parsedDate;
                        }
                        // Try parsing just the year if full date parsing fails
                        if (int.TryParse(originalDate.Substring(0, Math.Min(4, originalDate.Length)), out var year) && 
                            year > 1800 && year <= DateTime.Now.Year + 5)
                        {
                            _logger.LogInformation("Found ORIGINALDATE year in file for {ItemName}: {Year}", item.Name, year);
                            return new DateTime(year, 1, 1);
                        }
                    }

                    // Also check for ORIGINALYEAR
                    var originalYear = xiphTag.GetFirstField("ORIGINALYEAR");
                    if (!string.IsNullOrEmpty(originalYear) && 
                        int.TryParse(originalYear, out var origYear) && 
                        origYear > 1800 && origYear <= DateTime.Now.Year + 5)
                    {
                        _logger.LogInformation("Found ORIGINALYEAR in file for {ItemName}: {Year}", item.Name, origYear);
                        return new DateTime(origYear, 1, 1);
                    }
                }

                // Check for APE tags (used by some formats)
                var apeTag = file.GetTag(TagLib.TagTypes.Ape) as TagLib.Ape.Tag;
                if (apeTag != null)
                {
                    _logger.LogDebug("Found APE tag in file: {Path}", item.Path);
                    var originalDate = apeTag.GetItem("ORIGINALDATE");
                    if (originalDate != null && !string.IsNullOrEmpty(originalDate.ToString()))
                    {
                        var dateStr = originalDate.ToString();
                        _logger.LogInformation("Found ORIGINALDATE in APE tag with value: {Value}", dateStr);
                        if (DateTime.TryParse(dateStr, out var parsedDate))
                        {
                            return parsedDate;
                        }
                        if (int.TryParse(dateStr.Substring(0, Math.Min(4, dateStr.Length)), out var year) && 
                            year > 1800 && year <= DateTime.Now.Year + 5)
                        {
                            return new DateTime(year, 1, 1);
                        }
                    }
                }

                // Get ID3v2 tag for accessing TDOR/TDRL frames (MP3, M4A)
                var id3v2Tag = file.GetTag(TagLib.TagTypes.Id3v2) as TagLib.Id3v2.Tag;
                if (id3v2Tag != null)
                {
                    _logger.LogDebug("Found ID3v2 tag in file: {Path}", item.Path);
                    
                    // Check for TDOR (Original Release Time) - ID3v2.4 tag
                    var tdorFrame = TagLib.Id3v2.TextInformationFrame.Get(id3v2Tag, "TDOR", false);
                    if (tdorFrame != null && tdorFrame.Text.Length > 0)
                    {
                        var tdorText = tdorFrame.Text[0];
                        _logger.LogInformation("Found TDOR frame with value: {Value}", tdorText);
                        if (DateTime.TryParse(tdorText, out var tdorDate))
                        {
                            _logger.LogInformation("Found TDOR tag in file for {ItemName}: {Date}", item.Name, tdorDate);
                            return tdorDate;
                        }
                        // Try parsing just the year if full date parsing fails
                        if (int.TryParse(tdorText, out var tdorYear) && tdorYear > 1800 && tdorYear <= DateTime.Now.Year + 5)
                        {
                            _logger.LogInformation("Found TDOR year in file for {ItemName}: {Year}", item.Name, tdorYear);
                            return new DateTime(tdorYear, 1, 1);
                        }
                    }
                    // Check for TORY (Original Release Year) - ID3v2.3 tag
                    var toryFrame = TagLib.Id3v2.TextInformationFrame.Get(id3v2Tag, "TORY", false);
                    if (toryFrame != null && toryFrame.Text.Length > 0)
                    {
                        var toryText = toryFrame.Text[0];
                        _logger.LogInformation("Found TORY frame with value: {Value}", toryText);
                        if (int.TryParse(toryText, out var toryYear) && toryYear > 1800 && toryYear <= DateTime.Now.Year + 5)
                        {
                            _logger.LogInformation("Found TORY year in file for {ItemName}: {Year}", item.Name, toryYear);
                            return new DateTime(toryYear, 1, 1);
                        }
                    }

                    // Check for TXXX (user-defined text) frames with original year info
                    var userTextFrames = id3v2Tag.GetFrames<TagLib.Id3v2.UserTextInformationFrame>();
                    foreach (var userFrame in userTextFrames)
                    {
                        // Check for various descriptions that might contain original year
                        var description = userFrame.Description?.ToLowerInvariant() ?? "";
                        var isOriginalYearField = description.Contains("original") && 
                                                 (description.Contains("year") || description.Contains("date"));
                        
                        // Only process TXXX frames with explicit "original" in the description
                        if (isOriginalYearField && userFrame.Text.Length > 0)
                        {
                            if (int.TryParse(userFrame.Text[0], out var yearValue) && 
                                yearValue > 1800 && yearValue <= DateTime.Now.Year + 5)
                            {
                                // Only use if it's different from current production year
                                if (yearValue != item.ProductionYear)
                                {
                                    _logger.LogInformation("Found original year in TXXX frame (Description: '{Description}'): {Year}", 
                                        userFrame.Description ?? "(empty)", yearValue);
                                    return new DateTime(yearValue, 1, 1);
                                }
                            }
                        }
                    }

                    // Check for TDRL (Release Time) if TDOR not found
                    var tdrlFrame = TagLib.Id3v2.TextInformationFrame.Get(id3v2Tag, "TDRL", false);
                    if (tdrlFrame != null && tdrlFrame.Text.Length > 0)
                    {
                        var tdrlText = tdrlFrame.Text[0];
                        _logger.LogInformation("Found TDRL frame with value: {Value}", tdrlText);
                        if (DateTime.TryParse(tdrlText, out var tdrlDate))
                        {
                            _logger.LogInformation("Found TDRL tag in file for {ItemName}: {Date}", item.Name, tdrlDate);
                            return tdrlDate;
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("No ID3v2 tag found in file: {Path}", item.Path);
                }

                // As a last resort, check the standard Year tag if it contains an older date than the current premiere date
                // This avoids using remaster/rerelease years instead of original release years
                if (file.Tag.Year > 0 && item.PremiereDate.HasValue)
                {
                    var tagYear = (int)file.Tag.Year;
                    var currentYear = item.PremiereDate.Value.Year;
                    _logger.LogDebug("Found Year tag: {Year} (Current PremiereDate Year: {CurrentYear})", tagYear, currentYear);
                    
                    // Only use if it's a valid year AND older than the current premiere date
                    if (tagYear > 1800 && tagYear <= DateTime.Now.Year + 5 && tagYear < currentYear)
                    {
                        _logger.LogInformation("Using Year tag for {ItemName}: {Year} (older than current {CurrentYear})", 
                            item.Name, tagYear, currentYear);
                        return new DateTime(tagYear, 1, 1);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading metadata from file {Path}", item.Path);
            }
        }

        _logger.LogDebug("No original release date found for {ItemName}", item.Name);
        return null;
    }
}
