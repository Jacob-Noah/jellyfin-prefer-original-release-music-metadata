using System;
using System.Collections.Generic;
using Jellyfin.Plugin.PreferOriginalReleaseMusicMetadata.Configuration;
using Jellyfin.Plugin.PreferOriginalReleaseMusicMetadata.LibraryMonitor;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PreferOriginalReleaseMusicMetadata;

/// <summary>
/// The main plugin class.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private LibraryChangeMonitor? _libraryMonitor;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
    public Plugin(
        IApplicationPaths applicationPaths, 
        IXmlSerializer xmlSerializer,
        ILibraryManager libraryManager,
        ILoggerFactory loggerFactory)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;

        // Initialize library monitor for automatic processing
        try
        {
            var logger = loggerFactory.CreateLogger<LibraryChangeMonitor>();
            _libraryMonitor = new LibraryChangeMonitor(libraryManager, logger);
        }
        catch (Exception ex)
        {
            var pluginLogger = loggerFactory.CreateLogger<Plugin>();
            pluginLogger.LogError(ex, "Failed to initialize library monitor");
        }
    }

    /// <inheritdoc />
    public override string Name => "Prefer Original Release Music Metadata";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("7c8df481-8eee-4b3a-9a3a-5c5e5e5e5e5e");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = this.Name,
                EmbeddedResourcePath = string.Format("{0}.Configuration.configPage.html", GetType().Namespace)
            }
        };
    }
}
