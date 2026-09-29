using System;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using Widgy.Core;
using Widgy.Core.Attributes;
using Widgy.Core.Config;
using Widgy.Core.Enums;
using Widgy.Core.Rendering;
using Widgy.Widgets.Clock.Config;

namespace Widgy.Widgets.Clock
{
    [Widget("Clock", "Display current time")]
    [WidgetSize(4, 2)]
    [RefreshOnTick(1, TimeUnit.Seconds)]
    [Category("System")]
    public class ClockWidget : Widget<ClockConfig>
    {
        public override string Name => "Clock";
        public override string Description => "Display current time";
        public override string Category => "System";
        public override System.Drawing.Size[] SupportedSizes => new[] { new System.Drawing.Size(4, 2), new System.Drawing.Size(1, 1) };
        public override ClockConfig DefaultConfig => new ClockConfig();
        public override Type ConfigType => typeof(ClockConfig);

        public override Task RenderAsync(SKCanvas canvas, WidgetRenderContext context, CancellationToken token)
        {
            var config = (ClockConfig)context.Config;
            var theme = context.Theme;

            // Background is the panel surface color (design constant); the optional
            // TextColor config overrides the text color, not the background.
            canvas.Clear(theme.PanelBackgroundColor);

            float fontSize = (float)(context.PixelSize.Height * 0.3f * config.FontSize);
            float dateFontSize = (float)(context.PixelSize.Height * 0.15f * config.FontSize);

            var timeText = config.Format == "12h"
                ? context.Time.ToString("h:mm tt")
                : context.Time.ToString("HH:mm");

            using var timeTypeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;
            using var timeFont = new SKFont(timeTypeface, fontSize);

            var textBrush = string.IsNullOrEmpty(config.TextColor) ? theme.TextColor : ParseColor(config.TextColor);
            var paint = new SKPaint
            {
                Color = textBrush,
                IsAntialias = true
            };

            var x = context.PixelSize.Width / 2f;
            var y = context.PixelSize.Height / 2f - (fontSize / 4f);

            canvas.DrawText(timeText, x, y, SKTextAlign.Center, timeFont, paint);

            if (config.ShowDate)
            {
                using var dateTypeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;
                using var dateFont = new SKFont(dateTypeface, dateFontSize);

                var dateStr = context.Time.ToString("ddd MMM d, yyyy");
                canvas.DrawText(dateStr, x, y + dateFontSize + fontSize / 3f, SKTextAlign.Center, dateFont, paint);
            }

            paint.Dispose();
            return Task.CompletedTask;
        }

        private static SKColor ParseColor(string hexColor)
        {
            if (hexColor.StartsWith("#") && hexColor.Length == 7)
            {
                return SKColor.Parse(hexColor);
            }
            return SKColor.Parse("#000000");
        }
    }
}
