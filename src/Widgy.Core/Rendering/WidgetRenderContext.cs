using SkiaSharp;
using Widgy.Core.Config;

namespace Widgy.Core.Rendering;

public class WidgetRenderContext
{
    public SKCanvas Canvas { get; }
    public DateTime Time { get; }
    public System.Drawing.Size PixelSize { get; }
    public ThemeColors Theme { get; }
    public WidgetConfig Config { get; }
    public CancellationToken CancellationToken { get; }

    public WidgetRenderContext(
        SKCanvas canvas,
        DateTime time,
        System.Drawing.Size pixelSize,
        ThemeColors theme,
        WidgetConfig config,
        CancellationToken cancellationToken)
    {
        Canvas = canvas;
        Time = time;
        PixelSize = pixelSize;
        Theme = theme;
        Config = config;
        CancellationToken = cancellationToken;
    }
}
