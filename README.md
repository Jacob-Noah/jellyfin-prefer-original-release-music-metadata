# Prefer Original Release Music Metadata Plugin for Jellyfin

A Jellyfin plugin that forces Jellyfin to utilize original release dates over standard release dates in music metadata. This ensures that re-release CDs and compilations are sorted by their original release date rather than the re-release date.

## Features

- Configurable option to enable/disable the preference for original release dates
- Web-based configuration UI integrated with Jellyfin
- Compatible with Jellyfin 10.9.x

## Installation

### From Release

1. Download the latest release DLL from the [Releases](https://github.com/Jacob-Noah/jellyfin-prefer-original-release-music-metadata/releases) page
2. Place the DLL in your Jellyfin plugins directory:
   - Linux: `/var/lib/jellyfin/plugins/PreferOriginalReleaseMusicMetadata/`
   - Windows: `C:\ProgramData\Jellyfin\Server\plugins\PreferOriginalReleaseMusicMetadata\`
   - macOS: `/var/lib/jellyfin/plugins/PreferOriginalReleaseMusicMetadata/`
3. Restart Jellyfin

### From Source

See [CONTRIBUTING.md](CONTRIBUTING.md) for build instructions.

## Configuration

After installation, you can configure the plugin from the Jellyfin web interface:

1. Navigate to Dashboard → Plugins
2. Find "Prefer Original Release Music Metadata" in the list
3. Click on it to access the configuration page
4. Enable or disable the feature as needed
5. Save your changes

## How It Works

The plugin modifies how Jellyfin handles music metadata, specifically focusing on release dates. When enabled, it prioritizes the original release date field over the standard release date field in music files, ensuring proper chronological sorting even for re-releases and compilations.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for details on how to build and contribute to this project.
