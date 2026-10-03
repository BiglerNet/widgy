// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk;

namespace UrDeck.Engine.Rendering;

/// <summary>
/// The one paint path for a widget's surface, shared by the live window and snapshots: the host draws the card,
/// clips to its shape and then lets the widget draw. A widget that throws is drawn as a themed error card.
/// </summary>
public static class WidgetPainter
{
    /// <summary>
    /// Paints <paramref name="widget"/> on <paramref name="canvas"/>, whose origin is the top-left corner of the
    /// widget's card and whose size is <paramref name="size"/> (the card's size). The corners outside the card's
    /// rounded shape are left untouched.
    /// </summary>
    public static void Paint(
        IWidget widget,
        SKCanvas canvas,
        System.Drawing.Size size,
        Theme theme,
        DateTime time,
        CancellationToken cancellationToken)
    {
        var card = CardShape(size, theme);
        DrawCard(canvas, card, theme);

        int save = canvas.Save();
        try
        {
            canvas.ClipRoundRect(card, antialias: true);
            var context = new WidgetRenderContext(
                canvas, time, size, theme, ContentRect(size, theme), widget.Config, cancellationToken);
            widget.Render(context);
        }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Widget '{widget.Name}' render failed", ex);
            canvas.RestoreToCount(save);
            save = canvas.Save();
            canvas.ClipRoundRect(card, antialias: true);
            // Wipe whatever the widget managed to draw, then show the error on a fresh card.
            canvas.DrawColor(SKColors.Transparent, SKBlendMode.Src);
            DrawCard(canvas, card, theme);
            DrawError(canvas, size, theme, widget.Name, ex.Message);
        }
        finally
        {
            canvas.RestoreToCount(save);
        }
    }

    /// <summary>The card's rounded rectangle; the corner radius is limited to half the smaller side.</summary>
    internal static SKRoundRect CardShape(System.Drawing.Size size, Theme theme)
    {
        float radius = Math.Min(theme.CardRadius, Math.Min(size.Width, size.Height) / 2f);
        return new SKRoundRect(SKRect.Create(size.Width, size.Height), radius, radius);
    }

    /// <summary>The card's area inset by the theme's padding, never smaller than nothing.</summary>
    internal static SKRect ContentRect(System.Drawing.Size size, Theme theme)
    {
        float pad = Math.Min(theme.Padding, Math.Min(size.Width, size.Height) / 2f);
        return new SKRect(pad, pad, size.Width - pad, size.Height - pad);
    }

    private static void DrawCard(SKCanvas canvas, SKRoundRect card, Theme theme)
    {
        using var fill = new SKPaint { Color = theme.CardFill, IsAntialias = true };
        canvas.DrawRoundRect(card, fill);

        if (theme.CardBorderWidth <= 0 || theme.CardBorder.Alpha == 0)
            return;

        // The border is drawn inside the card's edge so it never extends past the surface.
        float half = theme.CardBorderWidth / 2f;
        var inner = new SKRoundRect(card.Rect, card.Radii[0].X, card.Radii[0].Y);
        inner.Deflate(half, half);
        using var border = new SKPaint
        {
            Color = theme.CardBorder,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = theme.CardBorderWidth,
        };
        canvas.DrawRoundRect(inner, border);
    }

    private static void DrawError(SKCanvas canvas, System.Drawing.Size size, Theme theme, string name, string message)
    {
        var content = ContentRect(size, theme);
        using var paint = new SKPaint { Color = theme.Critical, IsAntialias = true };

        float y = content.Top;
        y = DrawLine(canvas, paint, theme.GetTypeface(TextRole.Title), theme.TitleSize, content, y, name);
        DrawLine(canvas, paint, theme.GetTypeface(TextRole.Body), theme.BodySize, content, y + theme.BodySize * 0.5f, message);
    }

    /// <summary>Draws one left-aligned line, shrunk to the content width, and returns the y below it.</summary>
    private static float DrawLine(SKCanvas canvas, SKPaint paint, SKTypeface typeface, float size, SKRect content, float top, string text)
    {
        using var font = new SKFont(typeface, size) { Subpixel = true, Edging = SKFontEdging.Antialias };
        float width = font.MeasureText(text);
        if (width > content.Width && width > 0)
            font.Size = size * content.Width / width;

        float cap = font.Metrics.CapHeight > 0 ? font.Metrics.CapHeight : -font.Metrics.Ascent * 0.7f;
        canvas.DrawText(text, content.Left, top + cap, SKTextAlign.Left, font, paint);
        return top + cap;
    }
}
