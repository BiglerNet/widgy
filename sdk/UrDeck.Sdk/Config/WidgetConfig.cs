// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrDeck.Sdk;

/// <summary>
/// Base class for widget configuration. Derive from it to add widget-specific settings; the host
/// reads them from the page file next to the placement properties below.
/// </summary>
public class WidgetConfig
{
    [JsonPropertyName("typeId")]
    public string WidgetTypeId { get; set; } = "";

    [JsonPropertyName("col")]
    public int Col { get; set; }

    [JsonPropertyName("row")]
    public int Row { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1;

    [JsonPropertyName("parameters")]
    public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Widget-specific settings (e.g. Clock's "format") that the base type doesn't model. Preserved
    /// across load/save so the page file never loses them, and used to build the concrete config.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
