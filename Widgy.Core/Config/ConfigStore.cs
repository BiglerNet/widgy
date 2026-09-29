using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Widgy.Core.Config;

namespace Widgy.Core.Config
{
    public class PageConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Default";

        [JsonPropertyName("widgets")]
        public List<WidgetConfig> Widgets { get; set; } = new List<WidgetConfig>();

        [JsonPropertyName("background")]
        public object? Background { get; set; }
    }

    public class DockItemConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("command")]
        public string? Command { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }

    public class WidgyConfig
    {
        [JsonPropertyName("pages")]
        public List<PageConfig> Pages { get; set; } = new List<PageConfig>
        {
            new PageConfig { Name = "Default" }
        };

        [JsonPropertyName("activePage")]
        public int ActivePage { get; set; } = 0;

        [JsonPropertyName("dock")]
        public List<DockItemConfig> Dock { get; set; } = new List<DockItemConfig>();

        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "default-dark";

        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        [JsonPropertyName("monitor")]
        public int Monitor { get; set; } = 1;

        [JsonPropertyName("monitorName")]
        public string? MonitorName { get; set; }
    }

    public class ConfigStore
    {
        private WidgyConfig _config = new WidgyConfig();
        private string _configPath = "";
        private FileSystemWatcher? _watcher;
        private CancellationTokenSource? _debounceCts;

        public event Action<WidgyConfig>? ConfigChanged;

        public ConfigStore(string configPath)
        {
            _configPath = configPath;
            Load();
            WatchForConfigChanges();
        }

        public WidgyConfig Config => _config;

        public void Load()
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _config = JsonSerializer.Deserialize<WidgyConfig>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }) ?? new WidgyConfig();
                AttachWidgetRawJson(json);
            }
            else
            {
                _config = new WidgyConfig();
                Save();
            }
        }

        /// <summary>
        /// Attach each widget's raw JSON element to its WidgetConfig so plugins can
        /// deserialize their concrete config type (which has widget-specific properties
        /// the base WidgetConfig does not model).
        /// </summary>
        private void AttachWidgetRawJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array) return;

                int pi = 0;
                foreach (var pageEl in pages.EnumerateArray())
                {
                    if (pi >= _config.Pages.Count) break;
                    if (!pageEl.TryGetProperty("widgets", out var widgetsEl) || widgetsEl.ValueKind != JsonValueKind.Array) { pi++; continue; }
                    var widgets = _config.Pages[pi].Widgets;
                    int wi = 0;
                    foreach (var wEl in widgetsEl.EnumerateArray())
                    {
                        if (wi < widgets.Count) widgets[wi].RawJson = wEl.GetRawText();
                        wi++;
                    }
                    pi++;
                }
            }
            catch { }
        }

        public void Save()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            File.WriteAllText(_configPath, JsonSerializer.Serialize(_config, options));
        }

        private void WatchForConfigChanges()
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (string.IsNullOrEmpty(dir)) return;

            _watcher = new FileSystemWatcher(dir)
            {
                Filter = Path.GetFileName(_configPath),
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite
            };

            _watcher.Changed += OnConfigChanged;
        }

        private void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();

            Task.Delay(1000, _debounceCts.Token).ContinueWith(_ =>
            {
                _debounceCts?.Cancel();
                Load();
                ConfigChanged?.Invoke(_config);
            }, TaskScheduler.Default);
        }
    }
}
