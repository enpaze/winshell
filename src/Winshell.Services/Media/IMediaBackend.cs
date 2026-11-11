namespace Winshell.Media;

/// <summary>The media sessions Windows knows.</summary>
internal interface IMediaBackend : IDisposable
{
    IReadOnlyList<SmtcSession> Sessions { get; }

    event Action<SmtcSession>? SessionAdded;

    event Action<SmtcSession>? SessionRemoved;
}
