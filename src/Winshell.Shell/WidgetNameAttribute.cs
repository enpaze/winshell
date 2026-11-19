namespace Winshell;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class WidgetNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
