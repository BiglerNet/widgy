// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Sdk;

namespace UrDeck.Engine.Config;

public static class WidgetConfigExtensions
{
    /// <summary>
    /// Converts this config (typically a base <see cref="WidgetConfig"/> read from the page file) into
    /// the widget's concrete config type, picking up widget-specific settings from
    /// <see cref="WidgetConfig.ExtensionData"/>. Properties missing from the JSON keep the concrete type's defaults.
    /// </summary>
    public static WidgetConfig ToConcrete(this WidgetConfig config, Type configType)
    {
        if (configType == config.GetType())
            return config;
        if (!typeof(WidgetConfig).IsAssignableFrom(configType))
            throw new ArgumentException($"{configType} does not derive from {nameof(WidgetConfig)}.", nameof(configType));

        // Plugin config types live in collectible AssemblyLoadContexts. System.Text.Json caches type metadata
        // per resolver and pools equivalent options globally, so a shared resolver would pin the plugin and
        // block hot-reload unloading. A private resolver keeps that cache scoped to this call.
        var options = new JsonSerializerOptions(UrDeckJson.Options)
        {
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
        };
        string json = JsonSerializer.Serialize(config, config.GetType(), options);
        return (WidgetConfig)(JsonSerializer.Deserialize(json, configType, options)
            ?? throw new InvalidOperationException($"Could not create {configType.Name} from config."));
    }
}
