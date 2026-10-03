// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Windows.Threading;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Rendering;
using UrDeck.Sdk;

namespace UrDeck.Host;

/// <summary>
/// Hosts one widget instance in its own Skia surface, driven by the widget's declared refresh policy.
/// Widgets without a timer-based policy (OnEvent) render once and then only on explicit invalidation.
/// </summary>
internal sealed class WidgetView : SKElement, IDisposable
{
    private readonly IWidget _widget;
    private readonly Theme _theme;
    private readonly DispatcherTimer? _timer;
    private readonly CancellationTokenSource _cts = new();
    private bool _updating;
    private bool _hasPainted;

    public WidgetView(IWidget widget, WidgetDescriptor descriptor, Theme theme)
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

        // The first refresh always paints; later ones only when the widget says something changed.
        if (!_hasPainted || SafeNeedsRender())
            InvalidateVisual();
    }

    private bool SafeNeedsRender()
    {
        try
        {
            return _widget.NeedsRender(DateTime.Now);
        }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Widget '{_widget.Name}' NeedsRender failed; repainting", ex);
            return true;
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        WidgetPainter.Paint(
            _widget,
            canvas,
            new System.Drawing.Size(e.Info.Width, e.Info.Height),
            _theme,
            DateTime.Now,
            _cts.Token);
        _hasPainted = true;
    }

    public void Dispose()
    {
        _timer?.Stop();
        _cts.Cancel();
        _cts.Dispose();
        (_widget as IDisposable)?.Dispose();
    }
}
