using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Widgy.Core.Config;
using Widgy.Core.Diagnostics;
using Widgy.Core.Interfaces;

namespace Widgy.Core.Plugin
{
    public sealed class WidgetPluginLoader : IPluginService, IDisposable
    {
        private readonly WidgetRegistry _registry = new();
        private FileSystemWatcher? _watcher;
        private Timer? _debounce;
        private string? _pluginDirectory;

        public event Action? PluginsChanged;

        public WidgetRegistry Registry => _registry;

        public void ScanAndLoadPlugins(string pluginDirectory)
        {
            _pluginDirectory = Path.GetFullPath(pluginDirectory);
            Directory.CreateDirectory(_pluginDirectory);
            LoadAll();
            WatchForChanges();
        }

        // TODO(hot-reload): Assembly.LoadFrom can't unload or replace an already-loaded assembly, so a
        // rebuilt plugin DLL isn't picked up until restart. Move to a collectible AssemblyLoadContext
        // with shadow copies (tasks 4.5 / 8.6).
        public void ReloadPlugins()
        {
            _registry.Clear();
            LoadAll();
        }

        private void LoadAll()
        {
            if (_pluginDirectory == null) return;

            foreach (var dllPath in Directory.EnumerateFiles(_pluginDirectory, "*.dll"))
            {
                try
                {
                    LoadAssembly(Assembly.LoadFrom(dllPath));
                }
                catch (Exception ex)
                {
                    WidgyLog.Warn($"Failed to load plugin {Path.GetFileName(dllPath)}: {ex.Message}");
                }
            }
        }

        /// <summary>Registers every valid widget type in <paramref name="assembly"/>. Public so tests and hosts can register built-ins directly.</summary>
        public void LoadAssembly(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                WidgyLog.Warn($"Some types in {assembly.GetName().Name} failed to load: {ex.LoaderExceptions.FirstOrDefault()?.Message}");
                types = ex.Types.Where(t => t != null).ToArray()!;
            }

            foreach (var type in types)
            {
                if (!typeof(IWidget).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface) continue;

                var descriptor = WidgetDescriptor.TryCreate(type, out var reason);
                if (descriptor == null)
                {
                    WidgyLog.Warn($"Skipping widget {type.FullName}: {reason}");
                    continue;
                }

                _registry.Register(descriptor);
                WidgyLog.Info($"Registered widget '{descriptor.Id}' ({type.FullName}, {descriptor.Refresh} {descriptor.RefreshInterval})");
            }
        }

        private void WatchForChanges()
        {
            _debounce = new Timer(_ =>
            {
                try
                {
                    ReloadPlugins();
                    PluginsChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    WidgyLog.Error("Plugin reload failed", ex);
                }
            });

            _watcher = new FileSystemWatcher(_pluginDirectory!, "*.dll")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            };
            FileSystemEventHandler onChange = (_, _) => _debounce.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
            _watcher.Changed += onChange;
            _watcher.Created += onChange;
            _watcher.Deleted += onChange;
            _watcher.Renamed += (_, _) => _debounce.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
            _watcher.EnableRaisingEvents = true;
        }

        public IReadOnlyList<string> GetRegisteredWidgetTypes() => _registry.GetRegisteredWidgetTypes();

        public WidgetDescriptor? GetDescriptor(string typeId) => _registry.GetDescriptor(typeId);

        public IWidget? CreateWidget(WidgetConfig config) => _registry.CreateWidget(config);

        public void Dispose()
        {
            _watcher?.Dispose();
            _debounce?.Dispose();
        }
    }
}
