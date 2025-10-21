namespace Jellyfin.Plugin.PreferOriginalReleaseMusicMetadata.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : MediaBrowser.Model.Plugins.BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        EnablePreferOriginalRelease = true;
    }

    /// <summary>
    /// Gets or sets a value indicating whether to prefer original release dates.
    /// </summary>
    public bool EnablePreferOriginalRelease { get; set; }
}
