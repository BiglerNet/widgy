using System.IO;
using System.Windows;
using SkiaSharp;
using Widgy.Core.Config;
using Widgy.Core.Diagnostics;
using Widgy.Core.Interfaces;
using Widgy.Core.Plugin;
using Widgy.Core.Rendering;

namespace Widgy.Host
{
    public partial class App : Application
    {
        private ConfigStore? _configStore;
        private WidgetPluginLoader? _plugins;

        // Startup sequence per host-shell spec: monitor -> config -> plugins -> layout -> render loop.
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += (_, args) => WidgyLog.Error("Unhandled UI exception", args.Exception);

            var appDir = AppContext.BaseDirectory;
            WidgyLog.Info($"Widgy starting from {appDir}");

            var monitors = MonitorPlacement.GetMonitors();
            foreach (var m in monitors) WidgyLog.Info($"Monitor: {m}");

            _configStore = new ConfigStore(Path.Combine(appDir, "widgy-config.json"));

            _plugins = new WidgetPluginLoader();
            _plugins.ScanAndLoadPlugins(Path.Combine(appDir, "plugins"));

            var target = MonitorPlacement.Select(_configStore.Config, monitors);
            WidgyLog.Info($"Target monitor: {target}");

            var snapshotIndex = Array.IndexOf(e.Args, "--snapshot");
            if (snapshotIndex >= 0)
            {
                var exitCode = RunSnapshot(e.Args, snapshotIndex, target);
                Shutdown(exitCode);
                return;
            }

            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow = new MainWindow(_configStore, _plugins, target);
            MainWindow.Show();
        }

        /// <summary>
        /// <c>--snapshot out.png [--size WxH]</c>: renders the active page off-screen with the same layout and
        /// widget code as the live window, writes a PNG and exits. Defaults to the target monitor's resolution.
        /// </summary>
        private int RunSnapshot(string[] args, int index, MonitorInfo target)
        {
            try
            {
                var output = index + 1 < args.Length ? args[index + 1] : "widgy.snapshot.png";
                var width = target.Bounds.Width;
                var height = target.Bounds.Height;

                var sizeIndex = Array.IndexOf(args, "--size");
                if (sizeIndex >= 0 && sizeIndex + 1 < args.Length)
                {
                    var parts = args[sizeIndex + 1].Split('x', 'X');
                    width = int.Parse(parts[0]);
                    height = int.Parse(parts[1]);
                }

                var page = _configStore!.Config.CurrentPage;
                var widgets = new List<(WidgetConfig, IWidget)>();
                foreach (var config in page?.Widgets ?? new List<WidgetConfig>())
                {
                    var widget = _plugins!.CreateWidget(config);
                    if (widget == null) WidgyLog.Warn($"Snapshot: '{config.WidgetTypeId}' not registered");
                    else widgets.Add((config, widget));
                }

                using var bitmap = PageRenderer.RenderToBitmap(width, height, widgets, ThemeColors.DefaultDark, DateTime.Now);
                using var file = File.Create(output);
                bitmap.Encode(file, SKEncodedImageFormat.Png, 100);
                WidgyLog.Info($"Snapshot {width}x{height} with {widgets.Count} widget(s) written to {Path.GetFullPath(output)}");
                return 0;
            }
            catch (Exception ex)
            {
                WidgyLog.Error("Snapshot failed", ex);
                return 1;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _configStore?.Save(); }
            catch (Exception ex) { WidgyLog.Warn($"Could not save config on exit: {ex.Message}"); }
            _configStore?.Dispose();
            _plugins?.Dispose();
            base.OnExit(e);
        }
    }
}
