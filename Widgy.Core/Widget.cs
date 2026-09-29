using System;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using Widgy.Core.Config;
using Widgy.Core.Interfaces;
using Widgy.Core.Rendering;

namespace Widgy.Core
{
    public abstract class Widget<TConfig> : IWidget<TConfig> where TConfig : WidgetConfig, new()
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract string Category { get; }
        public abstract System.Drawing.Size[] SupportedSizes { get; }
        public abstract TConfig DefaultConfig { get; }
        public abstract Type ConfigType { get; }

        public abstract Task RenderAsync(SKCanvas canvas, WidgetRenderContext context, CancellationToken token);
    }
}
