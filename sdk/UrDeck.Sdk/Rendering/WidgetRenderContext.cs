// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk;

public class WidgetRenderContext
{
    public SKCanvas Canvas { get; }
    public DateTime Time { get; }
    public System.Drawing.Size PixelSize { get; }
    public Theme Theme { get; }
    /// <summary>The card's area inset by the theme's padding; where content normally goes.</summary>
    public SKRect ContentRect { get; }
    public WidgetConfig Config { get; }
    public CancellationToken CancellationToken { get; }

    public WidgetRenderContext(
        SKCanvas canvas,
        DateTime time,
        System.Drawing.Size pixelSize,
        Theme theme,
        SKRect contentRect,
        WidgetConfig config,
        CancellationToken cancellationToken)
    {
        Canvas = canvas;
        Time = time;
        PixelSize = pixelSize;
        Theme = theme;
        ContentRect = contentRect;
        Config = config;
        CancellationToken = cancellationToken;
    }
}
