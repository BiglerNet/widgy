using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SkiaSharp;
using Widgy.Core.Config;
using Widgy.Core.Diagnostics;
using Widgy.Core.Interfaces;
using Widgy.Core.Plugin;
using Widgy.Core.Rendering;

namespace Widgy.Host;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Disposed in OnExit, the WPF shutdown hook.")]
public partial class App : Application
{
    private ConfigStore? _configStore;
    private WidgetPluginLoader? _plugins;

    // Startup sequence per host-shell spec: monitor -> config -> plugins -> layout -> render loop.
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Software-only WPF composition: the hardware path allocates D3D surfaces for the full
        // borderless window (~60 MB private bytes on a 1100x3840 monitor). Widgets are already
        // software-rendered by Skia, so nothing is lost. Set WIDGY_HWRENDER=1 to opt back in.
        if (Environment.GetEnvironmentVariable("WIDGY_HWRENDER") != "1")
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        // Opt-in diagnostics: WIDGY_MEMLOG=1 logs process/GC memory 10 s after startup.
        if (Environment.GetEnvironmentVariable("WIDGY_MEMLOG") == "1")
        {
            var memTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            memTimer.Tick += (_, _) =>
            {
                memTimer.Stop();
                using var proc = System.Diagnostics.Process.GetCurrentProcess();
                WidgyLog.Info($"MEM ws={proc.WorkingSet64 / 1048576.0:0.#}MB private={proc.PrivateMemorySize64 / 1048576.0:0.#}MB " +
                              $"gcHeap={GC.GetTotalMemory(false) / 1048576.0:0.#}MB gcCommitted={GC.GetGCMemoryInfo().TotalCommittedBytes / 1048576.0:0.#}MB");
            };
            memTimer.Start();
        }
        DispatcherUnhandledException += (_, args) => WidgyLog.Error("Unhandled UI exception", args.Exception);

        string appDir = AppContext.BaseDirectory;
        WidgyLog.Info($"Widgy starting from {appDir}");

        var monitors = MonitorPlacement.GetMonitors();
        foreach (var m in monitors)
            WidgyLog.Info($"Monitor: {m}");

        _configStore = new ConfigStore(Path.Combine(appDir, "widgy-config.json"));

        _plugins = new WidgetPluginLoader();
        _plugins.ScanAndLoadPlugins(Path.Combine(appDir, "plugins"));

        var target = MonitorPlacement.Select(_configStore.Config, monitors);
        WidgyLog.Info($"Target monitor: {target}");

        int snapshotIndex = Array.IndexOf(e.Args, "--snapshot");
        if (snapshotIndex >= 0)
        {
            int exitCode = RunSnapshot(e.Args, snapshotIndex, target);
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
            string output = index + 1 < args.Length ? args[index + 1] : "widgy.snapshot.png";
            int width = target.Bounds.Width;
            int height = target.Bounds.Height;

            int sizeIndex = Array.IndexOf(args, "--size");
            if (sizeIndex >= 0 && sizeIndex + 1 < args.Length)
            {
                string[] parts = args[sizeIndex + 1].Split('x', 'X');
                width = int.Parse(parts[0], CultureInfo.InvariantCulture);
                height = int.Parse(parts[1], CultureInfo.InvariantCulture);
            }

            var page = _configStore!.Config.CurrentPage;
            var widgets = new List<(WidgetConfig, IWidget)>();
            foreach (var config in page?.Widgets ?? new List<WidgetConfig>())
            {
                var widget = _plugins!.CreateWidget(config);
                if (widget == null)
                    WidgyLog.Warn($"Snapshot: '{config.WidgetTypeId}' not registered");
                else
                    widgets.Add((config, widget));
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
        try
        { _configStore?.Save(); }
        catch (Exception ex) { WidgyLog.Warn($"Could not save config on exit: {ex.Message}"); }
        _configStore?.Dispose();
        _plugins?.Dispose();
        base.OnExit(e);
    }
}
