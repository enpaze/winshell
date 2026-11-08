namespace Winshell.Notifications;

/// <summary>Whether the shell may read the notifications in Notification Center.</summary>
public enum NotificationAccessStatus
{
    /// <summary>The user has not answered yet, or the first read has not finished.</summary>
    [PublicAPI] Unspecified,

    /// <summary>The user allowed access.</summary>
    [PublicAPI] Allowed,

    /// <summary>The user denied access. It can be allowed again in Windows Settings.</summary>
    [PublicAPI] Denied,

    /// <summary>Windows cannot give access, such as when the shell runs without a package identity.</summary>
    [PublicAPI] Unavailable,

    /// <summary>An access request is waiting for the user's answer.</summary>
    [PublicAPI] Requesting
}

/// <summary>Whether a Windows setting is on.</summary>
public enum NotificationState
{
    /// <summary>Windows does not say, or this version of Windows does not have the setting.</summary>
    [PublicAPI] Unknown,

    /// <summary>The setting is off.</summary>
    [PublicAPI] Off,

    /// <summary>The setting is on.</summary>
    [PublicAPI] On
}

/// <summary>Which notifications Windows shows, as do not disturb sets it.</summary>
public enum NotificationMode
{
    /// <summary>Windows does not say, or this version of Windows does not report it.</summary>
    [PublicAPI] Unknown = -1,

    /// <summary>Every notification shows.</summary>
    [PublicAPI] Unrestricted,

    /// <summary>Only notifications from priority apps and people show.</summary>
    [PublicAPI] PriorityOnly,

    /// <summary>Only alarms show.</summary>
    [PublicAPI] AlarmsOnly
}

internal sealed record IncomingToast(
    uint Id,
    string AppId,
    string AppName,
    string Title,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt = null,
    IAppLogoSource? Logo = null);

internal sealed record NotificationSnapshot(
    NotificationAccessStatus AccessStatus,
    IReadOnlyList<IncomingToast> Notifications,
    NotificationState Focus,
    NotificationMode Mode,
    string Error = "");
