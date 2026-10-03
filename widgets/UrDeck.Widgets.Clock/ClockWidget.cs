// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
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
    // The widest time the readout must fit, so its size does not change from minute to minute.
    private const string WidestTime = "88:88";

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
        var content = context.ContentRect;

        bool is12h = Config.Format == "12h";
        string timeText = is12h
            ? context.Time.ToString("h:mm", CultureInfo.InvariantCulture)
            : context.Time.ToString("HH:mm", CultureInfo.InvariantCulture);
        string dateText = context.Time.ToString("ddd MMM d, yyyy", CultureInfo.CurrentCulture);

        var options = new ReadoutOptions
        {
            Unit = is12h ? (context.Time.Hour >= 12 ? "PM" : "AM") : null,
            UnitPlacement = UnitPlacement.Baseline,
            WidestValue = WidestTime,
            Vertical = VerticalAlign.Bottom,
            ValueColor = Config.TextColor != null && SKColor.TryParse(Config.TextColor, out var custom) ? custom : null,
            Scale = (float)Config.FontSize,
        };

        // Time above date, the pair centered in the content rectangle: measure the date, give the time what is left,
        // then shift the whole block up by the space the time did not need.
        float dateHeight = 0f;
        float gap = 0f;
        if (Config.ShowDate)
        {
            dateHeight = TextLine.Measure(theme, content, dateText, TextStep.Title).Height;
            gap = theme.TitleSize;
        }

        var timeSlot = new SKRect(content.Left, content.Top, content.Right, Math.Max(content.Top + 1, content.Bottom - dateHeight - gap));
        var timeRect = Readout.Measure(theme, timeSlot, timeText, options);
        float blockHeight = timeRect.Height + gap + dateHeight;
        float shift = (content.Height - blockHeight) / 2f;

        int save = canvas.Save();
        canvas.Translate(0, -shift);
        Readout.Draw(canvas, theme, timeSlot, timeText, options);
        if (Config.ShowDate)
        {
            var dateSlot = new SKRect(content.Left, content.Bottom - dateHeight, content.Right, content.Bottom);
            TextLine.Draw(canvas, theme, dateSlot, dateText, TextStep.Title, TextEmphasis.Muted, vertical: VerticalAlign.Top);
        }
        canvas.RestoreToCount(save);
    }

    private static DateTime TruncateToMinute(DateTime time) =>
        new(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, time.Kind);
}
