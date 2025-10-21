# Contributing to Prefer Original Release Music Metadata Plugin

## Building the Plugin

This plugin is built using .NET 8.0. To build the plugin:

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build Instructions

1. Clone the repository:
   ```bash
   git clone https://github.com/Jacob-Noah/jellyfin-prefer-original-release-music-metadata.git
   cd jellyfin-prefer-original-release-music-metadata
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Build the project:
   ```bash
   dotnet build --configuration Release
   ```

4. The compiled plugin DLL will be located at:
   ```
   bin/Release/net8.0/Jellyfin.Plugin.PreferOriginalReleaseMusicMetadata.dll
   ```

## Installing the Plugin

1. Copy the compiled DLL to your Jellyfin plugins directory:
   - Linux: `/var/lib/jellyfin/plugins/PreferOriginalReleaseMusicMetadata/`
   - Windows: `C:\ProgramData\Jellyfin\Server\plugins\PreferOriginalReleaseMusicMetadata\`
   - macOS: `/var/lib/jellyfin/plugins/PreferOriginalReleaseMusicMetadata/`

2. Restart your Jellyfin server

3. The plugin configuration page will be available in Jellyfin's plugin settings

## Development

The plugin targets:
- **Jellyfin 10.11.0+** - Known compatible with Jellyfin 10.11.0+
- **.NET 8.0** - Target framework

## Code Style

The project uses:
- XML documentation comments for all public APIs
- Nullable reference types enabled
- Warnings treated as errors

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Build and test locally
5. Submit a pull request

## License

See the [LICENSE](LICENSE) file for details.
