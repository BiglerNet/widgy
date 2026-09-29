using System.Collections.Generic;
using Widgy.Core.Config;

namespace Widgy.Widgets.Clock.Config
{
    public class ClockConfig : WidgetConfig
    {
        public ClockConfig()
        {
            WidgetTypeId = "widgy.widgets.clock";
        }

        public string Format { get; set; } = "24h";
        public bool ShowDate { get; set; } = true;
        public string? TextColor { get; set; }
        public double FontSize { get; set; } = 1.0;
    }
}
