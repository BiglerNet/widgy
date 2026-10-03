// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SkiaSharp;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;

namespace UrDeck.Host;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Disposed in OnExit, the WPF shutdown hook.")]
public partial class App : Application
{
    private ConfigStore? _configStore;
    private WidgetPluginLoader? _plugins;
    private ThemeStore? _themes;

    // Startup sequence per host-shell spec: monitor -> config -> plugins -> layout -> render loop.
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Software-only WPF composition: the hardware path allocates D3D surfaces for the full
        // borderless window (~60 MB private bytes on a 1100x3840 monitor). Widgets are already
        // software-rendered by Skia, so nothing is lost. Set URDECK_HWRENDER=1 to opt back in.
        if (Environment.GetEnvironmentVariable("URDECK_HWRENDER") != "1")
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        // Opt-in diagnostics: URDECK_MEMLOG=1 logs process/GC memory 10 s after startup.
        if (Environment.GetEnvironmentVariable("URDECK_MEMLOG") == "1")
        {
            var memTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            memTimer.Tick += (_, _) =>
            {
                memTimer.Stop();
                using var proc = System.Diagnostics.Process.GetCurrentProcess();
                UrDeckLog.Info($"MEM ws={proc.WorkingSet64 / 1048576.0:0.#}MB private={proc.PrivateMemorySize64 / 1048576.0:0.#}MB " +
                              $"gcHeap={GC.GetTotalMemory(false) / 1048576.0:0.#}MB gcCommitted={GC.GetGCMemoryInfo().TotalCommittedBytes / 1048576.0:0.#}MB");
            };
            memTimer.Start();
        }
        DispatcherUnhandledException += (_, args) => UrDeckLog.Error("Unhandled UI exception", args.Exception);

        string appDir = AppContext.BaseDirectory;
        UrDeckLog.Info($"UrDeck starting from {appDir}");

        var monitors = MonitorPlacement.GetMonitors();
        foreach (var m in monitors)
            UrDeckLog.Info($"Monitor: {m}");

        _configStore = new ConfigStore(Path.Combine(appDir, "urdeck-config.json"));

        // User themes live in a themes folder next to the executable; the built-in ones are embedded in the engine.
        _themes = new ThemeStore(Path.Combine(appDir, "themes"));

        _plugins = new WidgetPluginLoader();
        _plugins.ScanAndLoadPlugins(Path.Combine(appDir, "plugins"));

        var target = MonitorPlacement.Select(_configStore.Config, monitors);
        UrDeckLog.Info($"Target monitor: {target}");

        int snapshotIndex = Array.IndexOf(e.Args, "--snapshot");
        if (snapshotIndex >= 0)
        {
            int exitCode = RunSnapshot(e.Args, snapshotIndex, target);
            Shutdown(exitCode);
            return;
        }

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        MainWindow = new MainWindow(_configStore, _plugins, _themes, target);
        MainWindow.Show();
    }

    /// <summary>
    /// <c>--snapshot out.png [--size WxH] [--theme name]</c>: renders the active page off-screen with the same layout and
    /// widget code as the live window, writes a PNG and exits. Defaults to the target monitor's resolution.
    /// </summary>
    private int RunSnapshot(string[] args, int index, MonitorInfo target)
    {
        try
        {
            string output = index + 1 < args.Length ? args[index + 1] : "urdeck.snapshot.png";
            int width = target.Bounds.Width;
            int height = target.Bounds.Height;

            int sizeIndex = Array.IndexOf(args, "--size");
            if (sizeIndex >= 0 && sizeIndex + 1 < args.Length)
            {
                string[] parts = args[sizeIndex + 1].Split('x', 'X');
                width = int.Parse(parts[0], CultureInfo.InvariantCulture);
                height = int.Parse(parts[1], CultureInfo.InvariantCulture);
            }

            int themeIndex = Array.IndexOf(args, "--theme");
            string themeName = themeIndex >= 0 && themeIndex + 1 < args.Length ? args[themeIndex + 1] : _configStore!.Config.Theme;
            var theme = _themes!.Load(themeName);

            var page = _configStore!.Config.CurrentPage;
            var widgets = new List<(WidgetConfig, IWidget)>();
            foreach (var config in page?.Widgets ?? new List<WidgetConfig>())
            {
                var widget = _plugins!.CreateWidget(config);
                if (widget == null)
                    UrDeckLog.Warn($"Snapshot: '{config.WidgetTypeId}' not registered");
                else
                    widgets.Add((config, widget));
            }

            using var bitmap = PageRenderer.RenderToBitmap(width, height, widgets, theme, DateTime.Now);
            using var file = File.Create(output);
            bitmap.Encode(file, SKEncodedImageFormat.Png, 100);
            UrDeckLog.Info($"Snapshot {width}x{height} with {widgets.Count} widget(s) written to {Path.GetFullPath(output)}");
            return 0;
        }
        catch (Exception ex)
        {
            UrDeckLog.Error("Snapshot failed", ex);
            return 1;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        { _configStore?.Save(); }
        catch (Exception ex) { UrDeckLog.Warn($"Could not save config on exit: {ex.Message}"); }
        _configStore?.Dispose();
        _plugins?.Dispose();
        base.OnExit(e);
    }
}
