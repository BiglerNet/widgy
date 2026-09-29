using System;
using System.Collections.Generic;
using System.Threading;
using SkiaSharp;
using Widgy.Core.Config;
using Widgy.Core.Interfaces;
using Widgy.Core.Layout;

namespace Widgy.Core.Rendering
{
    /// <summary>
    /// Renders a whole page of widgets onto a single canvas. The live host draws each widget into its own
    /// element instead; this path is for snapshots and tests, and uses the same layout and widget code.
    /// </summary>
    public static class PageRenderer
    {
        public static void Render(
            SKCanvas canvas,
            int widthPx,
            int heightPx,
            IReadOnlyList<(WidgetConfig Config, IWidget Widget)> widgets,
            ThemeColors theme,
            DateTime time)
        {
            canvas.Clear(theme.BackgroundColor);

            var layout = new GridLayoutManager(widthPx, heightPx)
                .RenderWidgetLayout(widgets.Select(w => w.Config).ToList(), new System.Drawing.Size(widthPx, heightPx));

            for (var i = 0; i < widgets.Count; i++)
            {
                var (config, widget) = widgets[i];
                if (!config.IsVisible) continue;

                var item = layout[i];
                var save = canvas.Save();
                canvas.Translate(item.Position.X, item.Position.Y);
                canvas.ClipRect(SKRect.Create(item.Size.Width, item.Size.Height));

                var ctx = new WidgetRenderContext(canvas, time, item.Size, theme, widget.Config, CancellationToken.None);
                WidgetPainter.RenderSafely(widget, ctx);

                canvas.RestoreToCount(save);
            }
        }

        public static SKBitmap RenderToBitmap(
            int widthPx,
            int heightPx,
            IReadOnlyList<(WidgetConfig Config, IWidget Widget)> widgets,
            ThemeColors theme,
            DateTime time)
        {
            var bitmap = new SKBitmap(widthPx, heightPx);
            using var canvas = new SKCanvas(bitmap);
            Render(canvas, widthPx, heightPx, widgets, theme, time);
            return bitmap;
        }
    }
}
