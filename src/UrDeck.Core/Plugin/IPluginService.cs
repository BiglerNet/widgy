using UrDeck.Core.Config;
using UrDeck.Core.Interfaces;

namespace UrDeck.Core.Plugin;

public interface IPluginService
{
    /// <summary>Raised (on a thread-pool thread) after plugins were reloaded because the plugin directory changed.</summary>
    event Action? PluginsChanged;

    void ScanAndLoadPlugins(string pluginDirectory);
    void ReloadPlugins();
    IReadOnlyList<string> GetRegisteredWidgetTypes();
    WidgetDescriptor? GetDescriptor(string typeId);
    IWidget? CreateWidget(WidgetConfig config);
}
