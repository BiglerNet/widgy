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

        public WidgetAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }
}
