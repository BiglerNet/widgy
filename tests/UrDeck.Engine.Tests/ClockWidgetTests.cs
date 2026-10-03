// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Widgets.Clock;
using UrDeck.Widgets.Clock.Config;
using Xunit;

namespace UrDeck.Engine.Tests;

public class ClockWidgetTests
{
    private static readonly DateTime NoonOnTheMinute = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Local);

    private static readonly LoadedTheme Loaded = new ThemeStore("").Load(ThemeStore.DefaultName);

    private static SKBitmap Paint(ClockWidget clock, DateTime time, int width = 400, int height = 200)
    {
        var size = new System.Drawing.Size(width, height);
        var bitmap = new SKBitmap(size.Width, size.Height);
        using var canvas = new SKCanvas(bitmap);
        using var theme = ThemeResolver.Resolve(Loaded, width / 4f);
        float pad = theme.Padding;
        clock.Render(new WidgetRenderContext(canvas, time, size, theme, new SKRect(pad, pad, width - pad, height - pad), clock.Config, CancellationToken.None));
        return bitmap;
    }

    private static bool HasPixel(SKBitmap bitmap, Func<SKColor, bool> match)
    {
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (match(bitmap.GetPixel(x, y)))
                    return true;
        return false;
    }

    private static int InkRows(SKBitmap bitmap)
    {
        int rows = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Alpha > 0)
                {
                    rows++;
                    break;
                }
        return rows;
    }

    [Fact]
    public void NeedsRender_IsTrueBeforeTheFirstPaint()
    {
        Assert.True(new ClockWidget().NeedsRender(NoonOnTheMinute));
    }

    [Fact]
    public void NeedsRender_IsFalseWithinTheSameMinute()
    {
        var clock = new ClockWidget();
        Paint(clock, NoonOnTheMinute.AddSeconds(5));

        Assert.False(clock.NeedsRender(NoonOnTheMinute.AddSeconds(6)));
        Assert.False(clock.NeedsRender(NoonOnTheMinute.AddSeconds(59)));
    }

    [Fact]
    public void NeedsRender_IsTrueWhenTheMinuteChanges()
    {
        var clock = new ClockWidget();
        Paint(clock, NoonOnTheMinute.AddSeconds(59));

        Assert.True(clock.NeedsRender(NoonOnTheMinute.AddSeconds(60)));
    }

    [Fact]
    public void NeedsRender_IsTrueAfterTheConfigChanges()
    {
        var clock = new ClockWidget();
        Paint(clock, NoonOnTheMinute);

        clock.Configure(new ClockConfig { Format = "12h" });

        Assert.True(clock.NeedsRender(NoonOnTheMinute.AddSeconds(1)));
    }

    [Fact]
    public void TextColorOverride_ColoursTheTime()
    {
        var clock = new ClockWidget();
        clock.Configure(new ClockConfig { TextColor = "#ff0000", ShowDate = false });
        using var bitmap = Paint(clock, NoonOnTheMinute);

        Assert.True(HasPixel(bitmap, c => c.Red > 200 && c.Green < 40 && c.Blue < 40 && c.Alpha > 200));
        Assert.False(HasPixel(bitmap, c => c.Alpha > 200 && c.Red > 200 && c.Green > 200 && c.Blue > 200));
    }

    [Fact]
    public void HiddenDate_DrawsLessThanWithDate()
    {
        var withDate = new ClockWidget();
        var noDate = new ClockWidget();
        noDate.Configure(new ClockConfig { ShowDate = false });
        using var a = Paint(withDate, NoonOnTheMinute);
        using var b = Paint(noDate, NoonOnTheMinute);

        // The time-only clock has a single block of ink; with the date there is a second line below it.
        Assert.True(InkRows(a) > InkRows(b));
    }

    [Fact]
    public void FontSizeBelowOne_DrawsSmallerTime()
    {
        var full = new ClockWidget();
        var half = new ClockWidget();
        full.Configure(new ClockConfig { ShowDate = false });
        half.Configure(new ClockConfig { ShowDate = false, FontSize = 0.5 });
        using var a = Paint(full, NoonOnTheMinute);
        using var b = Paint(half, NoonOnTheMinute);

        Assert.InRange(InkRows(b) / (double)InkRows(a), 0.4, 0.6);
    }

    [Fact]
    public void TwelveHour_ShowsPmUnit_AndRendersWithoutError()
    {
        var clock = new ClockWidget();
        clock.Configure(new ClockConfig { Format = "12h", ShowDate = false });
        using var pm = Paint(clock, new DateTime(2026, 1, 15, 19, 30, 0, DateTimeKind.Local));
        var am = new ClockWidget();
        am.Configure(new ClockConfig { Format = "12h", ShowDate = false });
        using var amBitmap = Paint(am, new DateTime(2026, 1, 15, 7, 30, 0, DateTimeKind.Local));

        Assert.NotEqual(pm.Bytes, amBitmap.Bytes);
    }
}
