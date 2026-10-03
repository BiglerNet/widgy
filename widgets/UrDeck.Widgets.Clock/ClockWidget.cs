// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Widgets.Clock.Config;

namespace UrDeck.Widgets.Clock;

[Widget("Clock", "Display current time", Id = "urdeck.widgets.clock")]
[WidgetSize(4, 2)]
[WidgetSize(4, 1)]
[WidgetSize(2, 1)]
[WidgetSize(1, 1)]
[RefreshOnTick(1, TimeUnit.Seconds)]
[Category("System")]
public class ClockWidget : Widget<ClockConfig>
{
    private static readonly SKTypeface TimeTypeface =
        SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        ?? SKTypeface.Default;

    private static readonly SKTypeface DateTypeface =
        SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Light, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        ?? SKTypeface.Default;

    // The minute that was last painted. The widget ticks every second to catch the rollover promptly,
    // but only repaints when the displayed minute (which also covers the date) changes.
    private DateTime? _lastRenderedMinute;

    public override bool NeedsRender(DateTime now) => _lastRenderedMinute != TruncateToMinute(now);

    protected override void OnConfigured() => _lastRenderedMinute = null;

    public override void Render(WidgetRenderContext context)
    {
        _lastRenderedMinute = TruncateToMinute(context.Time);

        var canvas = context.Canvas;
        var theme = context.Theme;
        float width = context.PixelSize.Width;
        float height = context.PixelSize.Height;

        DrawPanel(canvas, width, height, theme);

        var textColor = Config.TextColor != null && SKColor.TryParse(Config.TextColor, out var custom)
            ? custom
            : theme.TextColor;

        string timeText = Config.Format == "12h"
            ? context.Time.ToString("h:mm tt", CultureInfo.CurrentCulture)
            : context.Time.ToString("HH:mm", CultureInfo.CurrentCulture);
        string dateText = context.Time.ToString("ddd MMM d, yyyy", CultureInfo.CurrentCulture);

        float scale = (float)Math.Max(0.1, Config.FontSize);
        using var timeFont = new SKFont(TimeTypeface, height * 0.3f * scale) { Subpixel = true, Edging = SKFontEdging.Antialias };
        using var dateFont = new SKFont(DateTypeface, height * 0.15f * scale) { Subpixel = true, Edging = SKFontEdging.Antialias };

        // Shrink to fit narrow sizes (e.g. 1x1) with a margin on each side.
        float maxTextWidth = width * 0.85f;
        FitWidth(timeFont, timeText, maxTextWidth);
        if (Config.ShowDate)
            FitWidth(dateFont, dateText, maxTextWidth);

        // Vertically center the block using cap height (time) and x-height-ish ascent (date).
        float timeCap = CapHeight(timeFont);
        float dateCap = Config.ShowDate ? CapHeight(dateFont) : 0f;
        float gap = Config.ShowDate ? timeFont.Size * 0.3f : 0f;
        float blockHeight = timeCap + gap + dateCap;
        float top = (height - blockHeight) / 2f;
        float cx = width / 2f;

        using var timePaint = new SKPaint { Color = textColor, IsAntialias = true };
        canvas.DrawText(timeText, cx, top + timeCap, SKTextAlign.Center, timeFont, timePaint);

        if (Config.ShowDate)
        {
            using var datePaint = new SKPaint { Color = textColor.WithAlpha(0xCC), IsAntialias = true };
            canvas.DrawText(dateText, cx, top + timeCap + gap + dateCap, SKTextAlign.Center, dateFont, datePaint);
        }
    }

    private static DateTime TruncateToMinute(DateTime time) =>
        new(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, time.Kind);

    private static void DrawPanel(SKCanvas canvas, float width, float height, ThemeColors theme)
    {
        float inset = Math.Min(width, height) * 0.03f;
        float radius = Math.Min(width, height) * 0.08f;
        using var panel = new SKPaint { Color = theme.PanelBackgroundColor, IsAntialias = true };
        canvas.DrawRoundRect(SKRect.Create(inset, inset, width - inset * 2, height - inset * 2), radius, radius, panel);
    }

    private static void FitWidth(SKFont font, string text, float maxWidth)
    {
        float measured = font.MeasureText(text);
        if (measured > maxWidth && measured > 0)
            font.Size *= maxWidth / measured;
    }

    private static float CapHeight(SKFont font)
    {
        float cap = font.Metrics.CapHeight;
        return cap > 0 ? cap : -font.Metrics.Ascent * 0.7f;
    }
}
