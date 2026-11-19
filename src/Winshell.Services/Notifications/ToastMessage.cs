using System.Xml.Linq;

namespace Winshell.Notifications;

public sealed class ToastMessage : ObservableObject
{
    private readonly IEventLoop _loop;
    private readonly INotificationBackend _backend;
    private string _error = "";

    public ToastMessage()
        : this(EventLoop.Current, new NotificationBackend())
    {
    }

    internal ToastMessage(IEventLoop loop, INotificationBackend backend)
    {
        _loop = loop;
        _backend = backend;
    }

    public event Action<string>? Failed;

    public string AppId
    {
        get => Read(field);
        set => Set(ref field, Strings.OrEmpty(value));
    } = "";

    public string Title
    {
        get => Read(field);
        set => Set(ref field, Strings.OrEmpty(value));
    } = "";

    public string Body
    {
        get => Read(field);
        set => Set(ref field, Strings.OrEmpty(value));
    } = "";

    public string ActivationUri
    {
        get => Read(field);
        set => Set(ref field, Strings.OrEmpty(value));
    } = "";

    public string Tag
    {
        get => Read(field);
        set => Set(ref field, Strings.OrEmpty(value));
    } = "";

    public string Group
    {
        get => Read(field);
        set => Set(ref field, Strings.OrEmpty(value));
    } = "";

    public bool Silent
    {
        get => Read(field);
        set => Set(ref field, value);
    }

    public string Error => Read(_error);

    public ObservableList<ToastAction> Actions { get => Read(field); } = [];

    public bool Send()
    {
        if (Invalid() is { } problem)
        {
            ReportFailure(problem);
            return false;
        }

        Set(ref _error, "", nameof(Error));
        try
        {
            _backend.Show(AppId, BuildXml(), Tag, Group, ReportFailure);
            return true;
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
        {
            ReportFailure(e.Message);
            return false;
        }
    }

    private string? Invalid()
    {
        if (string.IsNullOrWhiteSpace(AppId))
            return "toast.appId must be an installed Windows AppUserModelID.";
        if (string.IsNullOrWhiteSpace(Title))
            return "toast.title must not be empty.";
        if (Tag.Length > 64 || Group.Length > 64)
            return "Toast tag and group must be at most 64 characters.";
        if (Group.Length > 0 && Tag.Length == 0)
            return "A toast group requires a tag.";
        if (Actions.Count > 5)
            return "Windows toasts support at most five actions.";
        if (Actions.Any(a => string.IsNullOrWhiteSpace(a.Text)))
            return "A toast action needs text.";
        if ((ActivationUri.Length > 0 && !IsAbsolute(ActivationUri)) || Actions.Any(a => !IsAbsolute(a.ActivationUri)))
            return "Toast activation requires an absolute protocol URI.";
        return null;
    }

    private static bool IsAbsolute(string uri)
    {
        return Uri.TryCreate(uri, UriKind.Absolute, out _);
    }

    private string BuildXml()
    {
        var root = new XElement("toast",
            new XElement("visual", new XElement("binding", new XAttribute("template", "ToastGeneric"),
                new XElement("text", Title), new XElement("text", Body))));
        if (ActivationUri.Length > 0)
            root.Add(new XAttribute("activationType", "protocol"), new XAttribute("launch", ActivationUri));
        if (Actions.Count > 0)
            root.Add(new XElement("actions", Actions.Select(a => new XElement("action",
                new XAttribute("content", a.Text), new XAttribute("activationType", "protocol"),
                new XAttribute("arguments", a.ActivationUri)))));
        if (Silent)
            root.Add(new XElement("audio", new XAttribute("silent", "true")));
        return root.ToString(SaveOptions.DisableFormatting);
    }

    private void ReportFailure(string message)
    {
        _loop.Post(() =>
        {
            Set(ref _error, string.IsNullOrWhiteSpace(message) ? "Windows could not send the toast." : message,
                nameof(Error));
            Failed?.Invoke(_error);
        });
    }
}

public sealed class ToastAction : ObservableObject
{
    public string Text
    {
        get => Read(field);
        set => Set(ref field, Strings.OrEmpty(value));
    } = "";

    public string ActivationUri
    {
        get => Read(field);
        set => Set(ref field, Strings.OrEmpty(value));
    } = "";
}

file static class Strings
{
    internal static string OrEmpty(string? value)
    {
        return value ?? "";
    }
}
