using SkiaSharp;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Widgy.Core.Rendering;
using Widgy.Widgets.Clock;
using Widgy.Widgets.Clock.Config;

const int W = 800, H = 400;
var theme = new ThemeColors(
    SKColors.White, SKColor.Parse("#0f0f1a"), SKColor.Parse("#4a9eff"),
    SKColor.Parse("#1a1a2e"), SKColor.Parse("#16213e"));

void InvokeWidget(SKCanvas canvas)
{
    var inst = new ClockWidget();
    var m = inst.GetType().GetMethod("RenderAsync");
    var ctx = new WidgetRenderContext(canvas, DateTime.Now, new Size(W, H), theme, new ClockConfig(), CancellationToken.None);
    var t = (Task)m.Invoke(inst, new object[] { canvas, ctx, CancellationToken.None });
    t.Wait(500);
}

int CountWhite(byte[] buf)
{
    int white = 0;
    for (int i = 0; i + 3 < buf.Length; i += 4)
    {
        int r = buf[i + 2], g = buf[i + 1], b = buf[i];
        if (r > 200 && g > 200 && b > 200) white++;
    }
    return white;
}

// CPU surface via allocated pinned buffer (no GPU/ANGLE at all)
{
    var info = new SKImageInfo(W, H, SKColorType.Rgba8888);
    int rowBytes = info.RowBytes;
    var buffer = new byte[rowBytes * H];
    var gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);
    SKSurface? surface = null;
    try
    {
        var pixmap = new SKPixmap(info, gch.AddrOfPinnedObject(), rowBytes);
        surface = SKSurface.Create(pixmap);
        Console.WriteLine($"CPU surface (allocated buffer): created={(surface != null ? "OK" : "NULL")}");
        if (surface != null)
        {
            using (surface)
            using (var canvas = surface.Canvas)
            {
                canvas.Clear(theme.BackgroundColor);
                InvokeWidget(canvas);
            }
            int w = CountWhite(buffer);
            Console.WriteLine($"   CPU surface buffer white={w}  {(w > 10 ? "TEXT OK" : "NO TEXT")}");
        }
    }
    finally { gch.Free(); }
}

Console.WriteLine("Done.");
