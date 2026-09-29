using System;
using System.Drawing;
using System.Collections.Generic;
using Widgy.Core.Config;

namespace Widgy.Core.Interfaces
{
    public interface IWidget<TConfig> where TConfig : WidgetConfig, new()
    {
        string Name { get; }
        string Description { get; }
        string Category { get; }
        System.Drawing.Size[] SupportedSizes { get; }
        TConfig DefaultConfig { get; }
        Type ConfigType { get; }
    }
}
