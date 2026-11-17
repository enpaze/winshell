namespace Winshell;

internal interface IInstanceControl
{
    ShellIpc Ipc { get; }

    string? Reload(string? program);

    void Quit();

    void Yield();

    void ShowBuildError(string error, string projectFile);

    void DismissNotice();

    Task<string> Inspect(string command, IReadOnlyList<string> args);
}
