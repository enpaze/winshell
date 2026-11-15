namespace Winshell.Tests;

/// <summary>
/// Tests that hide Explorer's taskbar. The desktop has one, so they must not run beside each other.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ExplorerTaskbarCollection
{
    public const string Name = "Explorer's taskbar";
}
