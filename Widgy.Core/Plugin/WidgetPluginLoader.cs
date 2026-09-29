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
        private readonly object _gate = new();
        private readonly List<LoadedPlugin> _loaded = new();
        private readonly List<string> _pendingDeletes = new();
        private static int _staleCleaned;

        public event Action? PluginsChanged;

        public WidgetRegistry Registry => _registry;

        public void ScanAndLoadPlugins(string pluginDirectory)
        {
            _pluginDirectory = Path.GetFullPath(pluginDirectory);
            Directory.CreateDirectory(_pluginDirectory);
            if (Interlocked.Exchange(ref _staleCleaned, 1) == 0) CleanStaleShadowDirs();
            lock (_gate) LoadAll();
            WatchForChanges();
        }

        /// <summary>Unloads every plugin context, clears the registry and loads the plugins folder afresh.</summary>
        public void ReloadPlugins()
        {
            lock (_gate)
            {
                _registry.Clear();
                UnloadAll();
                LoadAll();
            }
        }

        private void LoadAll()
        {
            if (_pluginDirectory == null) return;

            foreach (var dllPath in Directory.EnumerateFiles(_pluginDirectory, "*.dll"))
            {
                string? shadowDir = null;
                PluginLoadContext? context = null;
                try
                {
                    // Load from a copy so the original stays unlocked and can be overwritten by a rebuild.
                    shadowDir = Path.Combine(ShadowRoot, Environment.ProcessId.ToString(), Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(shadowDir);
                    var name = Path.GetFileNameWithoutExtension(dllPath);
                    var shadowDll = Path.Combine(shadowDir, name + ".dll");
                    File.Copy(dllPath, shadowDll);
                    foreach (var ext in new[] { ".pdb", ".deps.json" })
                    {
                        var side = Path.Combine(_pluginDirectory, name + ext);
                        if (File.Exists(side)) File.Copy(side, Path.Combine(shadowDir, name + ext));
                    }

                    context = new PluginLoadContext(shadowDll, _pluginDirectory);
                    _loaded.Add(new LoadedPlugin(context, shadowDir));
                    LoadAssembly(context.LoadFromAssemblyPath(shadowDll));
                }
                catch (Exception ex)
                {
                    WidgyLog.Warn($"Failed to load plugin {Path.GetFileName(dllPath)}: {ex.Message}");
                    // A context created for a failed plugin (e.g. not a .NET assembly) stays tracked and is unloaded on the next reload.
                    if (context == null && shadowDir != null) _pendingDeletes.Add(shadowDir);
                }
            }
        }

        private void UnloadAll()
        {
            var weakRefs = new List<WeakReference>();
            foreach (var plugin in _loaded)
            {
                weakRefs.Add(new WeakReference(plugin.Context));
                plugin.Context.Unload();
                _pendingDeletes.Add(plugin.ShadowDirectory);
            }
            _loaded.Clear();

            if (weakRefs.Count == 0)
                DeletePending();
            else
                Task.Run(() => AwaitCollection(weakRefs)); // diagnostic only; never blocks the reload
        }

        private void AwaitCollection(List<WeakReference> refs)
        {
            for (var i = 0; i < 10 && refs.Any(r => r.IsAlive); i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(50);
            }

            var alive = refs.Count(r => r.IsAlive);
            if (alive > 0)
                WidgyLog.Warn($"{alive} plugin context(s) were not collected after unload; something still references them.");
            lock (_gate) DeletePending();
        }

        private void DeletePending() => _pendingDeletes.RemoveAll(TryDeleteDirectory);

        private static bool TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ShadowRoot => Path.Combine(Path.GetTempPath(), "widgy-shadow");

        /// <summary>Removes shadow folders left behind by processes that are no longer running.</summary>
        private static void CleanStaleShadowDirs()
        {
            try
            {
                if (!Directory.Exists(ShadowRoot)) return;
                foreach (var dir in Directory.EnumerateDirectories(ShadowRoot))
                {
                    if (!int.TryParse(Path.GetFileName(dir), out var pid) || pid == Environment.ProcessId) continue;
                    if (!IsRunning(pid)) TryDeleteDirectory(dir);
                }
            }
            catch (Exception ex)
            {
                WidgyLog.Warn($"Shadow cleanup failed: {ex.Message}");
            }
        }

        private static bool IsRunning(int pid)
        {
            try
            {
                using var p = System.Diagnostics.Process.GetProcessById(pid);
                return !p.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch
            {
                return true; // can't tell; leave it alone
            }
        }

        private sealed record LoadedPlugin(PluginLoadContext Context, string ShadowDirectory);

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
            lock (_gate)
            {
                _registry.Clear();
                UnloadAll();
            }
        }
    }
}
