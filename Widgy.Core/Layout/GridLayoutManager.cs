using System;
using System.Collections.Generic;
using System.Drawing;
using Widgy.Core.Config;

namespace Widgy.Core.Layout
{
    public class GridLayoutManager
    {
        public double ScreenWidth { get; private set; }
        public double ScreenHeight { get; private set; }
        public double ColumnWidth => ScreenWidth / 4;
        public double RowHeight => ColumnWidth;


        public GridLayoutManager(double screenWidth, double screenHeight)
        {
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
        }

        public System.Drawing.Point ConvertToPixels(int col, int row, int width, int height)
        {
            return new System.Drawing.Point(
                (int)(col * ColumnWidth),
                (int)(row * RowHeight));
        }

        public Size ConvertSizeToPixels(int width, int height)
        {
            return new Size(
                (int)(width * ColumnWidth),
                (int)(height * RowHeight));
        }

        public bool ValidatePosition(int col, int width)
        {
            return col >= 0 && col + width <= 4 && width >= 1 && width <= 4;
        }

        public int ValidateHeight(int height)
        {
            return height >= 1 ? height : 1;
        }

        public List<WidgetLayoutItem> RenderWidgetLayout(List<WidgetConfig> widgets, Size screenDims)
        {
            ScreenWidth = screenDims.Width;
            ScreenHeight = screenDims.Height;
            var result = new List<WidgetLayoutItem>();

            foreach (var widget in widgets)
            {
                // Clamp width first so the column bound (4 - width) is valid.
                var clampedWidth = Clamp(1, widget.Width, 4);
                var clampedCol = Clamp(0, widget.Col, 4 - clampedWidth);
                var clampedRow = Math.Max(0, widget.Row);
                var clampedHeight = Math.Max(1, widget.Height);

                if (clampedCol != widget.Col || clampedWidth != widget.Width
                    || clampedRow != widget.Row || clampedHeight != widget.Height)
                {
                    Widgy.Core.Diagnostics.WidgyLog.Warn($"Widget {widget.WidgetTypeId} position clamped from ({widget.Col},{widget.Row})[{widget.Width}x{widget.Height}] to ({clampedCol},{clampedRow})[{clampedWidth}x{clampedHeight}]");
                }

                var pixelPos = new System.Drawing.Point(
                    (int)(clampedCol * ColumnWidth),
                    (int)(clampedRow * RowHeight));
                var pixelSize = new Size(
                    (int)(clampedWidth * ColumnWidth),
                    (int)(clampedHeight * RowHeight));

                var item = new WidgetLayoutItem
                {
                    WidgetTypeId = widget.WidgetTypeId,
                    Position = pixelPos,
                    Size = pixelSize,
                    GridSize = new Size(clampedWidth, clampedHeight)
                };
                result.Add(item);
            }

            return result;
        }

        private static int Clamp(int min, int value, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
