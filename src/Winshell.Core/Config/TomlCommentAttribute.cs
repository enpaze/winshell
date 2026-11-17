namespace Winshell;

[AttributeUsage(AttributeTargets.Property)]
public sealed class TomlCommentAttribute(string text) : Attribute
{
    public string Text { get; } = text;
}
