using System.Drawing;

namespace UrDeck.Core.Layout;

public class WidgetLayoutItem
{
    public string WidgetTypeId { get; set; } = "";
    public System.Drawing.Point Position { get; set; }
    public Size Size { get; set; }
    public Size GridSize { get; set; }
}
