using System.Windows.Threading;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using UrDeck.Core.Diagnostics;
using UrDeck.Core.Interfaces;
using UrDeck.Core.Plugin;
using UrDeck.Core.Rendering;

namespace UrDeck.Host;

/// <summary>
/// Hosts one widget instance in its own Skia surface, driven by the widget's declared refresh policy.
/// Widgets without a timer-based policy (OnEvent) render once and then only on explicit invalidation.
/// </summary>
internal sealed class WidgetView : SKElement, IDisposable
{
    private readonly IWidget _widget;
    private readonly ThemeColors _theme;
    private readonly DispatcherTimer? _timer;
    private readonly CancellationTokenSource _cts = new();
    private bool _updating;

    public WidgetView(IWidget widget, WidgetDescriptor descriptor, ThemeColors theme)
    {
        _widget = widget;
        _theme = theme;
        PaintSurface += OnPaintSurface;

        if (descriptor.RefreshInterval is { } interval && interval > TimeSpan.Zero)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = interval };
            _timer.Tick += (_, _) => _ = RefreshAsync();
        }

        Loaded += (_, _) =>
        {
            _ = RefreshAsync();
            _timer?.Start();
        };
        Unloaded += (_, _) => _timer?.Stop();
    }

    private async Task RefreshAsync()
    {
        if (_updating)
            return;
        _updating = true;
        try
        {
            await _widget.UpdateAsync(_cts.Token);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Widget '{_widget.Name}' update failed", ex);
        }
        finally
        {
            _updating = false;
        }
        InvalidateVisual();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var ctx = new WidgetRenderContext(
            canvas,
            DateTime.Now,
            new System.Drawing.Size(e.Info.Width, e.Info.Height),
            _theme,
            _widget.Config,
            _cts.Token);
        WidgetPainter.RenderSafely(_widget, ctx);
    }

    public void Dispose()
    {
        _timer?.Stop();
        _cts.Cancel();
        _cts.Dispose();
        (_widget as IDisposable)?.Dispose();
    }
}
