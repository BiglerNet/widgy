using System;

namespace Widgy.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class WidgetAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }
        public string? Category { get; set; }
        public Type? ConfigType { get; set; }

        /// <summary>
        /// Stable identifier used as <c>typeId</c> in widgy-config.json (e.g. "widgy.widgets.clock").
        /// Defaults to <see cref="Name"/> when not set; plugin authors should set it explicitly.
        /// </summary>
        public string? Id { get; set; }

        public WidgetAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }
}
