namespace Widgy.Core.Layout;

public class GridCell
{
    public int Col { get; }
    public int Row { get; }
    public int Width { get; }
    public int Height { get; }

    public GridCell(int col, int row, int width, int height)
    {
        Col = col;
        Row = row;
        Width = width;
        Height = height;
    }

    public bool Overlaps(GridCell other)
    {
        return Col < other.Col + other.Width
            && Col + Width > other.Col
            && Row < other.Row + other.Height
            && Row + Height > other.Row;
    }

    public bool Contains(int x, int y)
    {
        return x >= Col && x < Col + Width && y >= Row && y < Row + Height;
    }

    public override string ToString() => $"[{Col},{Row}]-[{Width}x{Height}]";
}
