namespace Winshell;

/// <summary>The comment written above a key the file does not have yet. A line break starts another comment line.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class TomlCommentAttribute(string text) : Attribute
{
    /// <summary>The comment's text, without the leading <c>#</c>.</summary>
    public string Text { get; } = text;
}
