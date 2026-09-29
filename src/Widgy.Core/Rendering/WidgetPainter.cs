using SkiaSharp;
using Widgy.Core.Diagnostics;
using Widgy.Core.Interfaces;

namespace Widgy.Core.Rendering;

public static class WidgetPainter
{
    /// <summary>
    /// Renders <paramref name="widget"/> into <paramref name="context"/>'s canvas. A widget that throws
    /// is drawn as a visible error tile rather than silently leaving the area blank.
    /// </summary>
    public static void RenderSafely(IWidget widget, WidgetRenderContext context)
    {
        var canvas = context.Canvas;
        int save = canvas.Save();
        try
        {
            widget.Render(context);
        }
        catch (Exception ex)
        {
            WidgyLog.Error($"Widget '{widget.Name}' render failed", ex);
            canvas.RestoreToCount(save);
            save = canvas.Save();
            DrawError(canvas, context.PixelSize.Width, context.PixelSize.Height, $"{widget.Name}: {ex.Message}");
        }
        finally
        {
            canvas.RestoreToCount(save);
        }
    }

    private static void DrawError(SKCanvas canvas, int width, int height, string message)
    {
        canvas.Clear(new SKColor(0x40, 0x10, 0x10));
        using var paint = new SKPaint { Color = SKColors.OrangeRed, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, Math.Max(12, height * 0.06f));
        canvas.DrawText(message, 12, font.Size + 12, SKTextAlign.Left, font, paint);
    }
}
