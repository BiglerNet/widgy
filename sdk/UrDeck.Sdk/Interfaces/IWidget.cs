// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk;

/// <summary>
/// Non-generic widget contract the host talks to. Widget authors normally derive from
/// <see cref="Widget{TConfig}"/> rather than implementing this directly.
/// </summary>
public interface IWidget
{
    string Name { get; }
    string Description { get; }
    string Category { get; }
    System.Drawing.Size[] SupportedSizes { get; }
    Type ConfigType { get; }
    WidgetConfig Config { get; }

    /// <summary>Applies a configuration; <paramref name="config"/> must be an instance of <see cref="ConfigType"/>.</summary>
    void Configure(WidgetConfig config);

    /// <summary>
    /// Refreshes widget data (network, sensors, ...). Called off the render path on each refresh
    /// before the widget is redrawn. Must not touch the canvas.
    /// </summary>
    Task UpdateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Draws the widget. Called on the UI thread; must be synchronous and fast. The canvas is only
    /// valid for the duration of the call.
    /// </summary>
    void Render(WidgetRenderContext context);
}

public interface IWidget<TConfig> : IWidget where TConfig : WidgetConfig, new()
{
    new TConfig Config { get; }
    TConfig DefaultConfig { get; }
}
