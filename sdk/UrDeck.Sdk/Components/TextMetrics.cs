// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

internal static class TextMetrics
{
    /// <summary>Reference size text is measured at; widths and heights scale linearly from it.</summary>
    public const float ReferenceSize = 100f;

    public static SKFont CreateFont(SKTypeface typeface, float size) =>
        new(typeface, size) { Subpixel = true, Edging = SKFontEdging.Antialias };

    public static float CapHeight(SKFont font)
    {
        float cap = font.Metrics.CapHeight;
        return cap > 0 ? cap : -font.Metrics.Ascent * 0.7f;
    }

    /// <summary>The widest advance among the digits 0-9, so every digit can take the same width.</summary>
    public static float DigitAdvance(SKFont font)
    {
        float widest = 0;
        for (char d = '0'; d <= '9'; d++)
            widest = Math.Max(widest, font.MeasureText(d.ToString()));
        return widest;
    }
}
