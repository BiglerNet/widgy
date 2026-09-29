using System;
using System.Collections.Generic;
using Widgy.Core.Config;

namespace Widgy.Core.Plugin
{
    public interface IPluginService
    {
        System.Threading.Tasks.Task ScanAndLoadPlugins(string pluginDirectory);
        System.Threading.Tasks.Task ReloadPlugins(string pluginDirectory);
        System.Collections.Generic.IReadOnlyList<string> GetRegisteredWidgetTypes();
        object? GetWidgetInstance(string typeId, WidgetConfig config);
    }
}
