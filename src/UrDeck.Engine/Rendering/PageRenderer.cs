// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;

namespace UrDeck.Engine.Rendering;

/// <summary>
/// Renders a whole page of widgets onto a single canvas. The live host draws each widget into its own
/// element instead; this path is for snapshots and tests, and uses the same layout, card and widget code.
/// </summary>
public static class PageRenderer
{
    public static void Render(
        SKCanvas canvas,
        int widthPx,
        int heightPx,
        IReadOnlyList<(WidgetConfig Config, IWidget Widget)> widgets,
        LoadedTheme loaded,
        DateTime time)
    {
        // Pixels are physical here, so the theme resolves against this canvas's own cell size.
        using var theme = ThemeResolver.Resolve(loaded, widthPx / 4f);
        canvas.Clear(theme.Background);

        var layout = new GridLayoutManager(widthPx, heightPx, loaded.Definition.Card!.Gap!.Value)
            .RenderWidgetLayout(widgets.Select(w => w.Config).ToList(), new System.Drawing.Size(widthPx, heightPx));

        for (int i = 0; i < widgets.Count; i++)
        {
            var (config, widget) = widgets[i];
            if (!config.IsVisible)
                continue;

            var item = layout[i];
            int save = canvas.Save();
            canvas.Translate(item.Position.X, item.Position.Y);
            WidgetPainter.Paint(widget, canvas, item.Size, theme, time, CancellationToken.None);
            canvas.RestoreToCount(save);
        }
    }

    public static SKBitmap RenderToBitmap(
        int widthPx,
        int heightPx,
        IReadOnlyList<(WidgetConfig Config, IWidget Widget)> widgets,
        LoadedTheme loaded,
        DateTime time)
    {
        var bitmap = new SKBitmap(widthPx, heightPx);
        using var canvas = new SKCanvas(bitmap);
        Render(canvas, widthPx, heightPx, widgets, loaded, time);
        return bitmap;
    }
}
