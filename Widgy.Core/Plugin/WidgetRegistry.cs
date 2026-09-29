using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Widgy.Core.Config;

namespace Widgy.Core.Plugin
{
    public class WidgetRegistry
    {
        private readonly Dictionary<string, (Type widgetType, Type configType, string description, string? category)> _registered = new();
        private readonly Dictionary<string, System.Drawing.Size[]> _supportedSizes = new();
        private readonly Dictionary<string, object> _instances = new();

        public void Register(string typeId, Type widgetType, Type configType, string description, string? category)
        {
            _registered[typeId] = (widgetType, configType, description, category);
        }

        public void AddSupportedSizes(string typeId, System.Drawing.Size[] sizes)
        {
            _supportedSizes[typeId] = sizes;
        }

        public void Clear()
        {
            _registered.Clear();
            _supportedSizes.Clear();
            _instances.Clear();
        }

        public IReadOnlyList<string> GetRegisteredWidgetTypes()
        {
            return new List<string>(_registered.Keys);
        }

        public Type? GetWidgetType(string typeId)
        {
            return _registered.TryGetValue(typeId, out var entry) ? entry.widgetType : null;
        }

        public object? GetWidgetInstance(string typeId, WidgetConfig config)
        {
            if (!_registered.TryGetValue(typeId, out var entry)) return null;

            var concrete = ToConcreteConfig(config, entry.configType);

            if (!_instances.TryGetValue(typeId, out var existing) || existing == null)
            {
                var instance = CreateInstance(entry.widgetType, concrete);
                if (instance == null) return null;
                _instances[typeId] = instance;
                return instance;
            }

            UpdateInstanceConfig(existing, concrete);
            return existing;
        }

        private static WidgetConfig ToConcreteConfig(WidgetConfig source, Type configType)
        {
            if (configType.IsInstanceOfType(source))
                return source;

            if (!configType.IsSubclassOf(typeof(WidgetConfig)))
                return source;

            // Preferred: deserialize the widget's raw JSON directly into the concrete type.
            // The raw JSON carries widget-specific properties (e.g. Clock's fontSize/format)
            // that the base WidgetConfig does not model.
            if (!string.IsNullOrEmpty(source.RawJson))
            {
                try
                {
                    var obj = JsonSerializer.Deserialize(source.RawJson, configType, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    if (obj is WidgetConfig wc) return wc;
                }
                catch { }
            }

            // Fallback: copy base fields into an uninitialized concrete instance.
            try
            {
                var target = (WidgetConfig)RuntimeHelpers.GetUninitializedObject(configType);
                target.LoadConfigJson(source.SaveConfigJson());
                return target;
            }
            catch
            {
                return source;
            }
        }

        private static object? CreateInstance(Type widgetType, WidgetConfig config)
        {
            // Preferred: constructor that accepts this widget's concrete config type
            var ctor = widgetType.GetConstructor(new[] { config.GetType() });
            if (ctor != null)
                return ctor.Invoke(new object[] { config });

            var instance = Activator.CreateInstance(widgetType);
            if (instance != null) UpdateInstanceConfig(instance, config);
            return instance;
        }

        private static void UpdateInstanceConfig(object instance, WidgetConfig config)
        {
            var set = instance.GetType().GetProperty("Config");
            if (set != null && set.CanWrite)
            {
                try { set.SetValue(instance, config); } catch { }
            }
        }

        public WidgetConfig GetWidgetConfig(string typeId, WidgetConfig config)
        {
            if (!_registered.TryGetValue(typeId, out var entry)) return config;
            return ToConcreteConfig(config, entry.configType);
        }

        public System.Drawing.Size[]? GetSupportedSizes(string typeId)
        {
            return _supportedSizes.TryGetValue(typeId, out var sizes) ? sizes : null;
        }
    }
}
