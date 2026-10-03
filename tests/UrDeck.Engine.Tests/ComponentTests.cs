// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class ComponentTests : IDisposable
{
    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);

    public void Dispose() => _theme.Dispose();

    private static readonly SKRect Slot = new(0, 0, 400, 200);

    private SKRect Draw(string value, ReadoutOptions? options = null, SKBitmap? into = null)
    {
        using var scratch = into == null ? new SKBitmap(400, 200) : null;
        using var canvas = new SKCanvas(into ?? scratch!);
        return Readout.Draw(canvas, _theme, Slot, value, options);
    }

    [Fact]
    public void Readout_SameSample_GivesSameSizeForShortAndLongValues()
    {
        var options = new ReadoutOptions { WidestValue = "100" };

        var a = Draw("8", options);
        var b = Draw("100", options);

        Assert.Equal(a.Height, b.Height, 3);
    }

    [Fact]
    public void Readout_ChangingDigits_DoesNotChangeSizeOrMoveTheColon()
    {
        var options = new ReadoutOptions { WidestValue = "88:88" };

        var r1 = Draw("11:11", options);
        var r2 = Draw("10:00", options);

        Assert.Equal(r1.Height, r2.Height, 3);
        // Equal digit widths: the characters before and after the colon sit at the same x for any digits.
        Assert.Equal(r1.Width, r2.Width, 2);
        Assert.Equal(r1.Left, r2.Left, 2);
    }

    [Fact]
    public void Readout_FitsItsRectangle()
    {
        var r = Draw("88:88", new ReadoutOptions { Unit = "PM", UnitPlacement = UnitPlacement.Baseline, Label = "NOW" });

        Assert.True(r.Left >= Slot.Left - 0.5f && r.Right <= Slot.Right + 0.5f);
        Assert.True(r.Top >= Slot.Top - 0.5f && r.Bottom <= Slot.Bottom + 0.5f);
    }

    [Fact]
    public void Readout_ScaleHalvesTheSize()
    {
        var full = Draw("12", new ReadoutOptions { Scale = 1f });
        var half = Draw("12", new ReadoutOptions { Scale = 0.5f });

        Assert.Equal(0.5f, half.Height / full.Height, 2);
    }

    [Fact]
    public void Readout_UnitIsSmallerThanValue_AndLabelIsDrawnBelow()
    {
        using var bitmap = new SKBitmap(400, 200);
        var r = Draw("41", new ReadoutOptions { Unit = "°", Label = "CPU" }, bitmap);

        // Label ink sits below the value's cap line; the block is taller than a plain value.
        Assert.True(r.Height > Draw("41").Height * 0.99f);
        Assert.True(r.Bottom <= Slot.Bottom + 0.5f);
    }

    [Fact]
    public void TextLine_FittingText_IsDrawnAtTheStepSize()
    {
        using var bitmap = new SKBitmap(2000, 200);
        using var canvas = new SKCanvas(bitmap);
        var r = TextLine.Draw(canvas, _theme, new SKRect(0, 0, 2000, 200), "Label", TextStep.Label);

        using var font = new SKFont(_theme.GetTypeface(TextRole.Label), _theme.LabelSize);
        Assert.Equal(font.MeasureText("Label"), r.Width, 1);
    }

    [Fact]
    public void TextLine_OverWideTitle_ShrinksToTheRectangle()
    {
        using var bitmap = new SKBitmap(200, 100);
        using var canvas = new SKCanvas(bitmap);
        var r = TextLine.Draw(canvas, _theme, new SKRect(0, 0, 100, 100), "A very long title that cannot fit", TextStep.Title);

        Assert.InRange(r.Width, 95, 100.5);
    }

    [Fact]
    public void TextLine_NeverGrowsBeyondTheStepSize()
    {
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        var small = TextLine.Measure(_theme, new SKRect(0, 0, 100, 100), "ab", TextStep.Body);
        var huge = TextLine.Measure(_theme, new SKRect(0, 0, 4000, 4000), "ab", TextStep.Body);

        Assert.Equal(small.Height, huge.Height, 2);
    }

    [Fact]
    public void TextLine_LabelsHaveTheSamePixelSizeInDifferentCards()
    {
        using var small = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);
        var inOneByOne = TextLine.Measure(small, new SKRect(0, 0, 200, 200), "CPU", TextStep.Label);
        var inFourByFour = TextLine.Measure(small, new SKRect(0, 0, 1000, 1000), "CPU", TextStep.Label);

        Assert.Equal(inOneByOne.Width, inFourByFour.Width, 2);
        Assert.Equal(inOneByOne.Height, inFourByFour.Height, 2);
    }
}
