// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using UrDeck.Engine.Layout;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

public class GridLayoutTests
{
    private static WidgetLayoutItem Layout(int sw, int sh, int col, int row, int w, int h)
    {
        var mgr = new GridLayoutManager(sw, sh);
        var cfg = new WidgetConfig { WidgetTypeId = "t", Col = col, Row = row, Width = w, Height = h };
        return mgr.RenderWidgetLayout(new List<WidgetConfig> { cfg }, new Size(sw, sh)).Single();
    }

    [Fact]
    public void SquareCells_1100()
    {
        var m = new GridLayoutManager(1100, 2000);
        Assert.Equal(275, m.ColumnWidth);
        Assert.Equal(275, m.RowHeight);
    }

    [Fact]
    public void FullWidthWidget_1100()
    {
        Assert.Equal(1100, Layout(1100, 2000, 0, 0, 4, 1).Size.Width);
    }

    [Fact]
    public void PositionInGridUnits_1100()
    {
        var i = Layout(1100, 2000, 0, 0, 2, 1);
        Assert.Equal(new Point(0, 0), i.Position);
        Assert.Equal(new Size(550, 275), i.Size);
    }

    // Spec text says pos (960,1080) size (1920,1620), i.e. RowHeight=540, contradicting RowHeight=ColumnWidth=960.
    [Fact]
    public void Conversion_3840x2160_FollowsFormula()
    {
        var i = Layout(3840, 2160, 1, 2, 2, 3);
        Assert.Equal(new Point(960, 1920), i.Position);
        Assert.Equal(new Size(1920, 2880), i.Size);
    }

    // Spec text says (7680, 540) but its own formula (RowHeight = ColumnWidth = 1920) gives 3840.
    [Fact]
    public void Conversion_7680x2160_FollowsFormula()
    {
        var i = Layout(7680, 2160, 0, 0, 4, 2);
        Assert.Equal(new Point(0, 0), i.Position);
        Assert.Equal(new Size(7680, 3840), i.Size);
    }

    [Fact]
    public void ResolutionChange_KeepsColumnWidth()
    {
        var m = new GridLayoutManager(1100, 3840);
        Assert.Equal(275, m.ColumnWidth);
        m.RenderWidgetLayout(new List<WidgetConfig>(), new Size(1100, 2160));
        Assert.Equal(275, m.ColumnWidth);
        Assert.Equal(2160, m.ScreenHeight);
    }

    [Fact]
    public void Overflow_ClampsColTo2()
    {
        var i = Layout(1100, 2000, 3, 0, 2, 1);
        Assert.Equal(550, i.Position.X);
        Assert.Equal(550, i.Size.Width);
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(5, 3)]
    [InlineData(9, 2)]
    public void WidthOverFour_ClampsToFour(int width, int col)
    {
        var i = Layout(1100, 2000, col, 0, width, 1);
        Assert.Equal(4, i.GridSize.Width);
        Assert.Equal(0, i.Position.X);
        Assert.Equal(1100, i.Size.Width);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void HeightBelowOne_ClampsToOne(int h)
    {
        var i = Layout(1100, 2000, 0, 0, 1, h);
        Assert.Equal(1, i.GridSize.Height);
        Assert.Equal(275, i.Size.Height);
    }

    [Fact]
    public void WidthBelowOne_ClampsToOne()
    {
        Assert.Equal(1, Layout(1100, 2000, 0, 0, 0, 1).GridSize.Width);
    }

    [Fact]
    public void NegativeColAndRow_ClampToZero()
    {
        var i = Layout(1100, 2000, -2, -5, 1, 1);
        Assert.Equal(new Point(0, 0), i.Position);
    }

    private static List<WidgetLayoutItem> LayoutAll(double gap, params (int Col, int Row, int W, int H)[] cells)
    {
        var configs = cells.Select(c => new WidgetConfig { WidgetTypeId = "t", Col = c.Col, Row = c.Row, Width = c.W, Height = c.H }).ToList();
        return new GridLayoutManager(1100, 3000, gap).RenderWidgetLayout(configs, new Size(1100, 3000));
    }

    [Fact]
    public void Gap_SeparatesNeighbouringCards()
    {
        var items = LayoutAll(0.04, (0, 0, 1, 1), (1, 0, 1, 1));

        Assert.Equal(11, items[1].Position.X - (items[0].Position.X + items[0].Size.Width));
        Assert.Equal(new Point(0, 0), items[0].CellPosition);
        Assert.Equal(new Size(275, 275), items[0].CellSize);
        // Half the gap (5.5 px) from the cell edge, rounded.
        Assert.InRange(items[0].Position.X, 5, 6);
    }

    [Fact]
    public void Gap_DoesNotDependOnWidgetSize()
    {
        var items = LayoutAll(0.04, (0, 0, 4, 2), (0, 2, 1, 1), (1, 2, 1, 1));

        int below = items[1].Position.Y - (items[0].Position.Y + items[0].Size.Height);
        int beside = items[2].Position.X - (items[1].Position.X + items[1].Size.Width);
        Assert.Equal(11, below);
        Assert.Equal(below, beside);
    }

    [Fact]
    public void ZeroGap_CardEqualsCell()
    {
        var item = LayoutAll(0, (1, 1, 2, 1)).Single();

        Assert.Equal(item.CellPosition, item.Position);
        Assert.Equal(item.CellSize, item.Size);
    }
}
