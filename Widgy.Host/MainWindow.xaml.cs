using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SkiaSharp;
using SkiaSharp.Views.WPF;
using Widgy.Core.Config;
using Widgy.Core.Layout;
using Widgy.Core.Plugin;
using Widgy.Core.Rendering;

namespace Widgy.Host
{
    public class MainWindow : Window
    {
        private GridLayoutManager _layoutManager;
        private readonly ConfigStore _configStore;
        private readonly WidgetPluginLoader _pluginLoader;
        private readonly System.Collections.Generic.List<WidgetCanvasHost> _hosts = new();
        private volatile bool _closing = false;
        private int _firstFrames = 0;
        private readonly Grid _mainGrid = new Grid { Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x2e)) };

        public MainWindow(GridLayoutManager lm, ConfigStore cs, WidgetPluginLoader pl)
        {
            _layoutManager = lm;
            _configStore = cs;
            _pluginLoader = pl;

            Width = System.Windows.SystemParameters.PrimaryScreenWidth;
            Height = System.Windows.SystemParameters.PrimaryScreenHeight;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            Content = _mainGrid;

            SizeChanged += OnResize;
            Loaded += OnLoaded;
        }

        private static void Log(string message)
        {
            try
            {
                var path = System.IO.Path.Combine(AppContext.BaseDirectory, "widgy.log");
                System.IO.File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
            }
            catch { }
        }

        private void OnLoaded(object s, EventArgs e)
        {
            Loaded -= OnLoaded;
            RebuildLayout();
            _ = Task.Run(Loop);
        }

        private void Loop()
        {
            while (!_closing)
            {
                try { RenderAll(); } catch (Exception ex) { Log("RenderAll: " + ex); }

                int ms = 1000;
                var cfg = _configStore.Config;
                if (cfg?.Pages != null && cfg.Pages.Count > 0 && cfg.Pages[cfg.ActivePage].Widgets != null)
                {
                    foreach (var w in cfg.Pages[cfg.ActivePage].Widgets)
                    {
                        if (w?.Parameters.TryGetValue("RefreshMs", out var rv) == true
                            && int.TryParse(rv, out var i) && i > 0 && i < ms)
                            ms = i;
                    }
                }

                Thread.Sleep(ms);
            }
        }

        private void RebuildLayout()
        {
            foreach (var h in _hosts)
            {
                if (_mainGrid.Children.Contains(h.Panel))
                    _mainGrid.Children.Remove(h.Panel);
            }
            _hosts.Clear();

            _layoutManager = new GridLayoutManager(ActualWidth, ActualHeight);
            var cfg = _configStore.Config;
            if (cfg?.Pages == null || cfg.Pages.Count == 0) return;

            var widgets = cfg.Pages[cfg.ActivePage].Widgets ?? new System.Collections.Generic.List<WidgetConfig>();
            Log($"RebuildLayout: window={ActualWidth}x{ActualHeight} widgets={widgets.Count}");
            foreach (var wc in widgets)
            {
                if (_pluginLoader.GetWidgetInstance(wc.WidgetTypeId, wc) == null) { Log($"RebuildLayout: SKIP {wc.WidgetTypeId} (not registered)"); continue; }

                var dim = _layoutManager.ConvertSizeToPixels(wc.Width, wc.Height);
                var pos = _layoutManager.ConvertToPixels(wc.Col, wc.Row, wc.Width, wc.Height);
                if (dim.Width < 16 || dim.Height < 16) { Log($"RebuildLayout: SKIP {wc.WidgetTypeId} (too small {dim.Width}x{dim.Height})"); continue; }

                var host = new WidgetCanvasHost((int)dim.Width, (int)dim.Height);
                Canvas.SetLeft(host.Panel, pos.X);
                Canvas.SetTop(host.Panel, pos.Y);
                _hosts.Add(host);
                _mainGrid.Children.Add(host.Panel);
                Log($"RebuildLayout: added {wc.WidgetTypeId} at {pos.X},{pos.Y} size {dim.Width}x{dim.Height}");
            }
        }

        private void RenderAll()
        {
            var cfg = _configStore.Config;
            if (cfg?.Pages == null || cfg.Pages.Count == 0) return;

            var theme = new ThemeColors(
                SKColors.White,
                SKColor.Parse("#0f0f1a"),
                SKColor.Parse("#4a9eff"),
                SKColor.Parse("#1a1a2e"),
                SKColor.Parse("#16213e"));

            var widgets = cfg.Pages[cfg.ActivePage].Widgets ?? new System.Collections.Generic.List<WidgetConfig>();
            var count = Math.Min(widgets.Count, _hosts.Count);

            // All SkiaSharp work (GPU surface create/draw/snapshot/pixel readback) happens on
            // this background thread. The WPF UI thread's D3D context interferes with SkiaSharp's
            // ANGLE readback (text was lost), so we keep the GPU work off the UI thread. Only the
            // final WriteableBitmap assignment is marshaled to the UI thread inside host.Render().
            for (int i = 0; i < count; i++)
                RenderOne(widgets[i], _hosts[i], theme);
        }

        private void RenderOne(WidgetConfig wc, WidgetCanvasHost host, ThemeColors theme)
        {
            try
            {
            var w = host.Width;
            var h = host.Height;
            if (w < 1 || h < 1) return;

            byte[] pixels = null;
            int rowBytes = 0;

            // CPU (raster) surface backed by an allocated pinned buffer. This avoids the
            // ANGLE/GPU backend entirely, which conflicts with the live WPF app's D3D render
            // context (GPU readback was losing the text). Drawing writes straight into our buffer.
            var info = new SKImageInfo(w, h, SKColorType.Rgba8888);
            rowBytes = info.RowBytes;
            pixels = new byte[rowBytes * h];
            var gch = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            bool surfaceOk = false;
            try
            {
                var pixmap = new SKPixmap(info, gch.AddrOfPinnedObject(), rowBytes);
                var surface = SKSurface.Create(pixmap);
                if (surface == null) { Log($"RenderOne {wc.WidgetTypeId}: SKSurface.Create(pixmap) returned null"); return; }
                surfaceOk = true;
                using (surface)
                using (var canvas = surface.Canvas)
                {
                    canvas.Clear(theme.BackgroundColor);

                    var inst = _pluginLoader.GetWidgetInstance(wc.WidgetTypeId, wc);
                    if (inst == null) return;

                    // Widgets cast context.Config to their concrete config type (e.g. ClockConfig).
                    var widgetConfig = _pluginLoader.GetWidgetConfig(wc.WidgetTypeId, wc);

                    var m = inst.GetType().GetMethod("RenderAsync");
                    if (m == null) return;

                    var ctx = new WidgetRenderContext(
                        canvas, DateTime.Now, new System.Drawing.Size(w, h), theme, widgetConfig, CancellationToken.None);
                    var t = (Task)m.Invoke(inst, new object[] { canvas, ctx, CancellationToken.None });
                    t.Wait(200);
                }
            }
            finally { gch.Free(); }
            if (!surfaceOk) return;

            if (pixels == null) { Log($"RenderOne {wc.WidgetTypeId}: no pixels produced"); return; }

            host.Render(pixels, w, h, rowBytes);
            if (_firstFrames++ == 1)
                Log($"RenderOne {wc.WidgetTypeId}: first frame rendered {w}x{h}");
            }
            catch (Exception ex)
            {
                Log($"RenderOne {wc.WidgetTypeId}: {ex}");
            }
        }

        private void OnResize(object s, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width >= 16 && e.NewSize.Height >= 16)
                RebuildLayout();
        }

        protected override void OnClosed(EventArgs e)
        {
            _closing = true;
            try { _configStore.Save(); } catch { }
            base.OnClosed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Application.Current.Shutdown();
            base.OnKeyDown(e);
        }
    }
}
