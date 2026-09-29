using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Widgy.Core.Config
{
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
        /// The widget's raw JSON as it appears in widgy-config.json. Captured by the
        /// config store so plugins can deserialize their concrete config type (which
        /// carries widget-specific properties the base type does not know about).
        /// </summary>
        [JsonIgnore]
        public string? RawJson { get; set; }

        public virtual string SaveConfigJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        public virtual void LoadConfigJson(string json)
        {
            var obj = JsonSerializer.Deserialize<WidgetConfig>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            if (obj != null)
            {
                WidgetTypeId = obj.WidgetTypeId;
                Col = obj.Col;
                Row = obj.Row;
                Width = obj.Width;
                Height = obj.Height;
                Parameters = obj.Parameters ?? new Dictionary<string, string>();
                IsVisible = obj.IsVisible;
            }
        }
    }
}
