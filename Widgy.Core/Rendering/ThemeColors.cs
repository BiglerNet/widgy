using SkiaSharp;

namespace Widgy.Core.Rendering
{
    public readonly record struct ThemeColors(
        SKColor TextColor,
        SKColor BackgroundColor,
        SKColor AccentColor,
        SKColor PanelBackgroundColor,
        SKColor PanelHeaderColor);
}
