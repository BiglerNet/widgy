namespace Widgy.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CategoryAttribute : Attribute
{
    public string Name { get; }

    public CategoryAttribute(string name)
    {
        Name = name;
    }
}
