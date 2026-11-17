namespace Winshell.Media;

internal interface IMediaBackend : IDisposable
{
    IReadOnlyList<SmtcSession> Sessions { get; }

    event Action<SmtcSession>? SessionAdded;

    event Action<SmtcSession>? SessionRemoved;
}
