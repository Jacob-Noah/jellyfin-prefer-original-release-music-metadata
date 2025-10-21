# Prefer Original Release Music Metadata Plugin for Jellyfin

A Jellyfin plugin that forces Jellyfin to utilize original release dates over standard release dates in music metadata. This ensures that re-release CDs and compilations are sorted by their original release date rather than the re-release date.

## Features

- Configurable option to enable/disable the preference for original release dates
- Web-based configuration UI integrated with Jellyfin
- Compatible with Jellyfin 10.10.7+

## Installation

### From Jellyfin Plugin Catalog (Recommended)

1. Open Jellyfin Dashboard
2. Locate where to add a plugin repository:
    - **Jellyfin 10.11.0+**: Navigate to **Plugins → Manage Repositories → New Repository**
    - **Jellyfin 10.10.7 and earlier**: Navigate to **Plugins → Catalog → ⚙️ (Settings) → + (Add)**
3. Enter the following details:
    - **Repository Name**: `Prefer Original Release Music Metadata` (or any name you prefer)
    - **Repository URL**: `https://raw.githubusercontent.com/Jacob-Noah/jellyfin-prefer-original-release-music-metadata/main/manifest.json`
5. Click **Save**
6. Locate the plugin in the catalog to install:
    - **Jellyfin 10.11.0+**: Navigate to **Plugins**
    - **Jellyfin 10.10.7 and earlier**: Navigate to **Plugins → Catalog**
7. Find "Prefer Original Release Music Metadata" in the list of all plugins
8. Click **Install**
9. Restart Jellyfin when prompted

### Manual Installation from Release

1. Download the latest release DLL from the [Releases](https://github.com/Jacob-Noah/jellyfin-prefer-original-release-music-metadata/releases) page
2. Create the plugin directory if it doesn't exist:
   - Linux: `/var/lib/jellyfin/plugins/Jellyfin.Plugin.PreferOriginalReleaseMusicMetadata/`
   - Windows: `C:\ProgramData\Jellyfin\Server\plugins\Jellyfin.Plugin.PreferOriginalReleaseMusicMetadata\`
   - macOS: `/var/lib/jellyfin/plugins/Jellyfin.Plugin.PreferOriginalReleaseMusicMetadata/`
3. Place the DLL in the directory you created
4. Restart Jellyfin

### From Source

See [CONTRIBUTING.md](CONTRIBUTING.md) for build instructions.

## Configuration

After installation, you can configure the plugin from the Jellyfin web interface:

1. Navigate to Dashboard → Plugins
2. Find "Prefer Original Release Music Metadata" in the list
3. Click on it to access the configuration page
4. Enable or disable the feature as needed
5. Save your changes

### Processing Your Music Library

The plugin provides a scheduled task to apply original release dates to your music:

1. Navigate to Dashboard → Scheduled Tasks
2. Find "Apply Original Release Date Metadata" in the task list
3. Click "Run" to process your music library
4. Monitor the progress in the task details

**When to run the task:**
- After initial plugin installation (to process your existing music)
- After adding new music to your library
- After updating metadata tags on existing files
- Set up an automatic schedule in Scheduled Tasks if you prefer

**Smart Processing with Persistent Cache:**
The task uses intelligent file-based caching to track which items it has already processed. This means:
- **First run**: Processes all music items in your library
- **Subsequent runs**: Only processes new items or items with changed metadata
- **Memory efficient**: Cache is stored on disk, not in RAM
- **Persistent**: Cache survives Jellyfin restarts
- **Much faster**: Subsequent runs are dramatically faster, even with large libraries

> **💡 Performance Tip:**  
> The first run examines every album and track in your library. For large libraries (several thousand albums), this initial run could probably take 30+ minutes. However, subsequent runs are much faster due to persistent caching - only new or changed items are processed. The cache file is stored in your plugin data folder and uses minimal disk space (within a few MB even for larger libraries). Progress can be monitored in the Scheduled Tasks dashboard.

## How It Works

This plugin modifies how Jellyfin handles music metadata internally. **It does not modify, overwrite, or alter your actual music file metadata in any way.**

The plugin reads original release date information from your music files and updates how Jellyfin sorts and displays them:

1. **Metadata Detection**: Reads original release date information from music file metadata tags:
   - TDOR (ID3v2.4 Original Release Year tag)
   - TORY (ID3v2.3 Original Release Year tag)
   - TDRL (ID3v2.4 Release Date tag)
   - OriginalReleaseDate custom field
   - OriginalYear / OriginalDate tags found in APE and Vorbis formats (FLAC, OGG)

2. **Jellyfin Database Update**: When original release date metadata is found, the plugin updates Jellyfin's internal database to use the original release date for sorting and display instead of the standard release date

3. **Scheduled Task**: Provides a scheduled task ("Apply Original Release Date Metadata") that can be run manually or on a schedule to process your music library

4. **Smart Caching**: Tracks processed items and only reprocesses when metadata changes, providing excellent performance even with large libraries

5. **Configuration**: Fully configurable through the Jellyfin web interface - you can enable or disable the feature at any time. Disabling the plugin will not revert changes; you would need to refresh metadata from your files. The scheduled task has no default schedule, so you can set it up exactly when it works best for you

### Example Use Case

If you have a remastered album from 2024 that was originally released in 1975, and your music files contain the original release year in the TDOR tag, the plugin will:
- Update Jellyfin's database to display the album as released in 1975 instead of 2024
- Sort the album chronologically with other albums from 1975 in Jellyfin's interface
- Maintain the integrity of your music timeline in Jellyfin

## Usage Guide

### Tagging Your Music Files

To get the most out of this plugin, ensure your music files include original release date metadata. The plugin reads this information but never modifies your files.

#### Recommended Tagging Tools

**Automated Music Management:**
- **Lidarr**: Automated music collection manager with MusicBrainz integration
  - Automatically tags music with original release dates for most re-release editions in my experience
  - Integrates with streaming clients, download clients and music taggers
  - Website: https://lidarr.audio/

**Manual Tagging Tools:**

**Windows:**
- **MusicBrainz Picard** (Recommended): Automatically retrieves and tags original release dates
  - Download: https://picard.musicbrainz.org/
- **Mp3tag**: Free, supports TDOR and custom fields
  - Download: https://www.mp3tag.de/en/

**macOS:**
- **MusicBrainz Picard**: Cross-platform, automatic metadata
- **Kid3**: Free, supports all ID3v2.4 tags including TDOR
  - Download: https://kid3.kde.org/

**Linux:**
- **MusicBrainz Picard**: Best option for automatic tagging
- **EasyTAG**: GTK-based, supports TDOR
- **Beets**: Command-line music organizer
  - Website: https://beets.io/

#### Tagging Best Practices

1. Use **MusicBrainz Picard** for initial tagging - it automatically retrieves original release dates
2. Verify the TDOR field is populated (format: `YYYY` or `YYYY-MM-DD`)
3. After tagging your files, scan your library in Jellyfin
4. Run the "Apply Original Release Date Metadata" scheduled task

### Troubleshooting

**Dates Not Changing:**
- Verify your music files have TDOR, OriginalReleaseDate, OriginalYear or OriginalDate tags using a tagging tool
- Check Jellyfin logs (Dashboard → Logs) for entries from "OriginalReleaseDatePostScanTask"
- Ensure the plugin is enabled in the configuration
- Run the scheduled task manually from Dashboard → Scheduled Tasks

**Plugin Not Working:**
- Verify the plugin appears in Dashboard → Plugins
- Check that the "Enable Prefer Original Release Date" checkbox is enabled in the configuration
- Restart Jellyfin after installing or updating the plugin

**Performance with Large Libraries:**
- The initial run may take time with large libraries
- Monitor progress in Dashboard → Scheduled Tasks
- Subsequent runs will be faster

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Any contributions you see fit are welcome. Please see [CONTRIBUTING.md](CONTRIBUTING.md) for details on how to build and contribute to this project.
