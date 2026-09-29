using SkiaSharp;

namespace Widgy.Core.Rendering;

public readonly record struct ThemeColors(
    SKColor TextColor,
    SKColor BackgroundColor,
    SKColor AccentColor,
    SKColor PanelBackgroundColor,
    SKColor PanelHeaderColor)
{
    public static ThemeColors DefaultDark { get; } = new(
        SKColors.White,
        SKColor.Parse("#0f0f1a"),
        SKColor.Parse("#4a9eff"),
        SKColor.Parse("#1a1a2e"),
        SKColor.Parse("#16213e"));
}
