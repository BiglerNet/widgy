// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Widgets.Clock;
using UrDeck.Widgets.Clock.Config;
using Xunit;

namespace UrDeck.Engine.Tests;

public class ClockWidgetTests
{
    private static readonly DateTime NoonOnTheMinute = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Local);

    private static void Paint(ClockWidget clock, DateTime time)
    {
        var size = new System.Drawing.Size(200, 100);
        using var bitmap = new SKBitmap(size.Width, size.Height);
        using var canvas = new SKCanvas(bitmap);
        clock.Render(new WidgetRenderContext(canvas, time, size, ThemeColors.DefaultDark, clock.Config, CancellationToken.None));
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
}
