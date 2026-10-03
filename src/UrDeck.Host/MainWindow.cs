// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SkiaSharp;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;

namespace UrDeck.Host;

public sealed class MainWindow : Window
{
    private readonly ConfigStore _configStore;
    private readonly WidgetPluginLoader _plugins;
    private readonly ThemeStore _themes;
    private readonly Canvas _surface = new();
    private readonly List<WidgetView> _views = new();
    private readonly List<DispatcherTimer> _pendingRetargets = new();
    private MonitorInfo _target;
    private bool _rebuildPending;
    private LoadedTheme _loadedTheme;
    private Theme? _theme;
    private LayoutKey _lastLayout;

    /// <summary>What a built page depends on: the window size, the display scaling and the active theme.</summary>
    private readonly record struct LayoutKey(System.Drawing.Size Screen, double DpiScale, LoadedTheme? Theme);

    internal MainWindow(ConfigStore configStore, WidgetPluginLoader plugins, ThemeStore themes, MonitorInfo target)
    {
        _configStore = configStore;
        _plugins = plugins;
        _themes = themes;
        _target = target;
        _loadedTheme = LoadTheme();

        Title = "UrDeck";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ApplyBackground();
        Content = _surface;

        SourceInitialized += (_, _) => MonitorPlacement.Cover(this, _target.Bounds);
        // Moving onto a monitor with a different DPI makes WPF resize the window by the DPI ratio;
        // re-apply the exact physical bounds once that settles.
        DpiChanged += (_, e) =>
        {
            UrDeckLog.Info($"DPI changed {e.OldDpi.DpiScaleX:0.##} -> {e.NewDpi.DpiScaleX:0.##}");
            Dispatcher.BeginInvoke(() => MonitorPlacement.Cover(this, _target.Bounds));
        };
        Loaded += (_, _) => LogPlacement();
        SizeChanged += (_, _) => ScheduleRebuild();

        _configStore.ConfigChanged += _ => Dispatcher.BeginInvoke(() =>
        {
            // Themes are re-read on every config reload, so editing a theme and saving the config applies it.
            _loadedTheme = LoadTheme();
            ApplyBackground();
            Retarget("config changed");
            ScheduleRebuild(force: true);
        });
        _plugins.PluginsChanged += () => Dispatcher.BeginInvoke(() =>
        {
            // Drop views holding old plugin instances now (not at the next idle rebuild) so the old
            // plugin contexts can be collected, then clear WPF's static cache that also pins them.
            DisposeViews();
            WpfAssemblyCache.EvictCollectibleAssemblies();
            ScheduleRebuild(force: true);
        });

        // Monitors can come back from sleep/hot-plug in any order and settle their DPI late, so
        // re-evaluate the target a few times after each change instead of trusting the first event.
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    private LoadedTheme LoadTheme()
    {
        var loaded = _themes.Load(_configStore.Config.Theme);
        UrDeckLog.Info($"Theme '{loaded.Name}' selected");
        return loaded;
    }

    private void ApplyBackground()
    {
        var c = SKColor.Parse(_loadedTheme.Definition.Colors!.Background);
        Background = new SolidColorBrush(Color.FromRgb(c.Red, c.Green, c.Blue));
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() => ScheduleRetargets("display settings changed"));

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
            Dispatcher.BeginInvoke(() => ScheduleRetargets("resumed from sleep"));
    }

    private void ScheduleRetargets(string reason)
    {
        foreach (var t in _pendingRetargets)
            t.Stop();
        _pendingRetargets.Clear();

        Retarget(reason);
        foreach (var delay in new[] { TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(5) })
        {
            var timer = new DispatcherTimer { Interval = delay };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                _pendingRetargets.Remove(timer);
                Retarget($"{reason} (+{delay.TotalSeconds:0.#}s)");
            };
            _pendingRetargets.Add(timer);
            timer.Start();
        }
    }

    /// <summary>Re-selects the target monitor from config and moves the window if it isn't covering it exactly.</summary>
    private void Retarget(string reason)
    {
        var monitors = MonitorPlacement.GetMonitors();
        if (monitors.Count == 0)
            return;

        var target = MonitorPlacement.Select(_configStore.Config, monitors);
        var actual = MonitorPlacement.GetWindowBounds(this);
        if (target == _target && actual.Equals(target.Bounds))
            return;

        UrDeckLog.Info($"Retarget ({reason}): {target}; window was at {actual.X},{actual.Y} {actual.Width}x{actual.Height}");
        _target = target;
        MonitorPlacement.Cover(this, target.Bounds);
        LogPlacement();
    }

    private void LogPlacement()
    {
        var actual = MonitorPlacement.GetWindowBounds(this);
        var dpi = VisualTreeHelper.GetDpi(this);
        var bounds = _target.Bounds;
        UrDeckLog.Info($"Window at {actual.X},{actual.Y} {actual.Width}x{actual.Height} physical " +
                      $"(target {bounds.X},{bounds.Y} {bounds.Width}x{bounds.Height}), " +
                      $"{ActualWidth:0.#}x{ActualHeight:0.#} DIPs, scale {dpi.DpiScaleX:0.##}");
        if (!actual.Equals(bounds))
            UrDeckLog.Warn("Window bounds do not match the target monitor.");
    }

    /// <summary>Coalesces layout rebuilds (resize, DPI and placement changes arrive in bursts).</summary>
    private void ScheduleRebuild(bool force = false)
    {
        if (force)
            _lastLayout = default;
        if (_rebuildPending)
            return;
        _rebuildPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _rebuildPending = false;
            RebuildLayout();
        });
    }

    private void RebuildLayout()
    {
        var screen = new System.Drawing.Size((int)ActualWidth, (int)ActualHeight);
        double dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var key = new LayoutKey(screen, dpiScale, _loadedTheme);
        if (key == _lastLayout)
            return;
        _lastLayout = key;

        // Views first, so nothing still paints with the old theme's typefaces when they are released.
        DisposeViews();
        _theme?.Dispose();
        _theme = null;

        var page = _configStore.Config.CurrentPage;
        if (page == null || screen.Width < 16 || screen.Height < 16)
            return;

        // Layout is computed in DIPs; each SKElement then renders at the monitor's physical resolution, so the theme
        // is resolved against the physical size of one grid cell.
        _theme = ThemeResolver.Resolve(_loadedTheme, (float)(screen.Width * dpiScale / 4));
        var layout = new GridLayoutManager(screen.Width, screen.Height, _loadedTheme.Definition.Card!.Gap!.Value)
            .RenderWidgetLayout(page.Widgets, screen);

        for (int i = 0; i < page.Widgets.Count; i++)
        {
            var config = page.Widgets[i];
            if (!config.IsVisible)
                continue;

            var descriptor = _plugins.GetDescriptor(config.WidgetTypeId);
            if (descriptor == null)
            {
                UrDeckLog.Warn($"Widget type '{config.WidgetTypeId}' is not registered; leaving its cell empty. " +
                              $"Registered: [{string.Join(", ", _plugins.GetRegisteredWidgetTypes())}]");
                continue;
            }

            try
            {
                var widget = _plugins.CreateWidget(config)!;
                var item = layout[i];
                var view = new WidgetView(widget, descriptor, _theme)
                {
                    Width = item.Size.Width,
                    Height = item.Size.Height,
                };
                Canvas.SetLeft(view, item.Position.X);
                Canvas.SetTop(view, item.Position.Y);
                _surface.Children.Add(view);
                _views.Add(view);
                UrDeckLog.Info($"Placed '{config.WidgetTypeId}' at grid ({config.Col},{config.Row}) {config.Width}x{config.Height} " +
                              $"-> {item.Position.X},{item.Position.Y} {item.Size.Width}x{item.Size.Height} DIPs");
            }
            catch (Exception ex)
            {
                UrDeckLog.Error($"Failed to create widget '{config.WidgetTypeId}'", ex);
            }
        }
    }

    private void DisposeViews()
    {
        foreach (var view in _views)
            view.Dispose();
        _views.Clear();
        _surface.Children.Clear();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
        base.OnKeyDown(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        // SystemEvents are static; unsubscribe or the window leaks.
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        foreach (var t in _pendingRetargets)
            t.Stop();
        DisposeViews();
        _theme?.Dispose();
        base.OnClosed(e);
    }
}
