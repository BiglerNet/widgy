using System.Text.Json;
using System.Text.Json.Serialization;

namespace Widgy.Core.Config;

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

    /// <summary>
    /// Converts this config (typically a base <see cref="WidgetConfig"/> read from the page file) into
    /// the widget's concrete config type, picking up widget-specific settings from <see cref="ExtensionData"/>.
    /// Properties missing from the JSON keep the concrete type's defaults.
    /// </summary>
    public WidgetConfig ToConcrete(Type configType)
    {
        if (configType == GetType())
            return this;
        if (!typeof(WidgetConfig).IsAssignableFrom(configType))
            throw new ArgumentException($"{configType} does not derive from {nameof(WidgetConfig)}.", nameof(configType));

        // Plugin config types live in collectible AssemblyLoadContexts. System.Text.Json caches type metadata
        // per resolver and pools equivalent options globally, so a shared resolver would pin the plugin and
        // block hot-reload unloading. A private resolver keeps that cache scoped to this call.
        var options = new JsonSerializerOptions(WidgyJson.Options)
        {
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
        };
        string json = JsonSerializer.Serialize(this, GetType(), options);
        return (WidgetConfig)(JsonSerializer.Deserialize(json, configType, options)
            ?? throw new InvalidOperationException($"Could not create {configType.Name} from config."));
    }

    public virtual string SaveConfigJson() => JsonSerializer.Serialize(this, GetType(), WidgyJson.Options);

    public virtual void LoadConfigJson(string json)
    {
        var obj = JsonSerializer.Deserialize<WidgetConfig>(json, WidgyJson.Options);
        if (obj == null)
            return;
        WidgetTypeId = obj.WidgetTypeId;
        Col = obj.Col;
        Row = obj.Row;
        Width = obj.Width;
        Height = obj.Height;
        Parameters = obj.Parameters ?? new Dictionary<string, string>();
        IsVisible = obj.IsVisible;
        ExtensionData = obj.ExtensionData;
    }
}

public static class WidgyJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
