namespace Winshell.Commands;

/// <summary>
/// Names in the widget record, <c>widgets.props</c>. The package targets read the item, the metadata and the property,
/// and <c>Winshell.props</c> keeps those three as its own literals. The source, ref and commit are only the CLI's.
/// </summary>
internal static class WidgetRecord
{
    internal const string Item = "WinshellWidget";

    internal const string Metadata = "Type";

    internal const string Property = "WinshellWidgetsFile";

    internal const string Source = "WinshellSource";

    internal const string Ref = "WinshellRef";

    internal const string Commit = "WinshellCommit";
}
