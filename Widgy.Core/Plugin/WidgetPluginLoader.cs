using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Widgy.Core.Attributes;
using Widgy.Core.Interfaces;
using Widgy.Core.Config;

namespace Widgy.Core.Plugin
{
    public class WidgetPluginLoader
    {
        private readonly WidgetRegistry _registry = new WidgetRegistry();
        private FileSystemWatcher? _watcher;
        private CancellationTokenSource? _debounceCts;

        public async Task ScanAndLoadPlugins(string pluginDirectory)
        {
            if (!Directory.Exists(pluginDirectory))
            {
                Directory.CreateDirectory(pluginDirectory);
            }

            foreach (var dllPath in Directory.EnumerateFiles(pluginDirectory, "*.dll"))
            {
                try
                {
                    var assembly = System.Reflection.Assembly.LoadFrom(dllPath);
                    PluginLog($"LoadFrom {Path.GetFileName(dllPath)} -> {assembly.FullName}");
                    await LoadAssembly(assembly);
                }
                catch (Exception ex)
                {
                    PluginLog($"Failed to load plugin {dllPath}: {ex}");
                    System.Diagnostics.Debug.WriteLine($"Failed to load plugin {dllPath}: {ex.Message}");
                }
            }

            WatchForChanges(pluginDirectory);
        }

        internal static void PluginLog(string message)
        {
            try
            {
                var path = System.IO.Path.Combine(AppContext.BaseDirectory, "widgy.log");
                System.IO.File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] [plugin] {message}\n");
            }
            catch { }
        }

        public async Task ReloadPlugins(string pluginDirectory)
        {
            _registry.Clear();

            foreach (var dllPath in Directory.EnumerateFiles(pluginDirectory, "*.dll"))
            {
                try
                {
                    var assembly = System.Reflection.Assembly.LoadFrom(dllPath);
                    await LoadAssembly(assembly);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to reload plugin {dllPath}: {ex.Message}");
                }
            }
        }

        private async Task LoadAssembly(System.Reflection.Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (Exception ex)
            {
                PluginLog($"GetTypes failed on {assembly.FullName}: {ex}");
                return;
            }

            foreach (var type in types)
            {
                if (!type.IsClass || type.IsAbstract) continue;

                var interfaces = type.GetInterfaces();
                var widgetInterface = interfaces.FirstOrDefault(i =>
                    (i.Name == "IWidget`1" && i.Namespace?.StartsWith("Widgy.Core.Interfaces") == true));

                if (widgetInterface == null) continue;

                PluginLog($"Candidate {type.FullName} interfaces=[{string.Join(", ", interfaces.Select(i => i.FullName))}] iwidgetAsm={widgetInterface.Assembly.FullName}");

                var widgetAttr = (WidgetAttribute?)System.Reflection.CustomAttributeExtensions.GetCustomAttribute<WidgetAttribute>(type);
                if (widgetAttr == null)
                {
                    PluginLog($"SKIP {type.Name}: no [Widget] attr. custom attrs=[{string.Join(", ", type.GetCustomAttributesData().Select(a => a.Constructor?.DeclaringType?.FullName))}]");
                    continue;
                }

                var configType = widgetAttr.ConfigType;
                if (configType == null)
                {
                    var genericArg = widgetInterface.GetGenericArguments()[0];
                    configType = genericArg;
                }

                _registry.Register(widgetAttr.Name, type, configType, widgetAttr.Description, widgetAttr.Category);
                PluginLog($"REGISTERED {widgetAttr.Name} type={type.FullName} config={configType.FullName}");

                var categoryAttr = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<CategoryAttribute>(type);
                var widgetSizeAttrs = type.GetCustomAttributes(typeof(WidgetSizeAttribute), true)
                    .Cast<WidgetSizeAttribute>()
                    .Select(s => new System.Drawing.Size(s.Width, s.Height))
                    .ToArray();

                _registry.AddSupportedSizes(widgetAttr.Name, widgetSizeAttrs);
            }
        }

        private void WatchForChanges(string pluginDirectory)
        {
            _watcher = new FileSystemWatcher(pluginDirectory)
            {
                Filter = "*.dll",
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };

            _watcher.Changed += OnPluginChanged;
            _watcher.Created += OnPluginChanged;
            _watcher.Deleted += OnPluginDeleted;
        }

        private void OnPluginChanged(object sender, FileSystemEventArgs e)
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();

            Task.Delay(500, _debounceCts.Token).ContinueWith(async _ =>
            {
                _debounceCts?.Cancel();
                try
                {
                    await ReloadPlugins(e.FullPath.Substring(0, e.FullPath.LastIndexOf('\\')));
                }
                catch { }
            }, TaskScheduler.Default);
        }

        private void OnPluginDeleted(object sender, FileSystemEventArgs e)
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();

            Task.Delay(500, _debounceCts.Token).ContinueWith(async _ =>
            {
                _debounceCts?.Cancel();
                try
                {
                    await ReloadPlugins(e.FullPath.Substring(0, e.FullPath.LastIndexOf('\\')));
                }
                catch { }
            }, TaskScheduler.Default);
        }

        public IReadOnlyList<string> GetRegisteredWidgetTypes()
        {
            return _registry.GetRegisteredWidgetTypes();
        }

        public object? GetWidgetInstance(string typeId, WidgetConfig config)
        {
            return _registry.GetWidgetInstance(typeId, config);
        }

        public Widgy.Core.Config.WidgetConfig GetWidgetConfig(string typeId, Widgy.Core.Config.WidgetConfig config)
        {
            return _registry.GetWidgetConfig(typeId, config);
        }
    }
}
