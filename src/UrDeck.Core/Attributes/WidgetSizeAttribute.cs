namespace UrDeck.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class WidgetSizeAttribute : Attribute
{
    public int Width { get; }
    public int Height { get; }

    public WidgetSizeAttribute(int width, int height)
    {
        Width = width;
        Height = height;
    }
}
