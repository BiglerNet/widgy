namespace UrDeck.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RefreshOnEventAttribute : Attribute
{
    public string EventName { get; }

    public RefreshOnEventAttribute(string eventName)
    {
        EventName = eventName;
    }
}
