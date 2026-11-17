using System.Text.RegularExpressions;

namespace Winshell;

internal static partial class CrashRedaction
{
    private const string Mask = "[redacted]";

    public static string Redact(string text)
    {
        text = UrlUserInfo().Replace(text, m => m.Groups["scheme"].Value + Mask + "@");
        text = UrlQuery().Replace(text, m => m.Groups["url"].Value + "?" + Mask);
        text = AuthorizationHeader().Replace(text, m => m.Groups["name"].Value + Mask);
        text = SchemeToken().Replace(text, m => m.Groups["scheme"].Value + " " + Mask);
        text = PassphraseLine().Replace(text, m => m.Groups["name"].Value + Mask);
        text = Assignment().Replace(text, m => m.Groups["key"].Value + m.Groups["sep"].Value + Mask);
        text = KnownTokenShape().Replace(text, Mask);
        text = Jwt().Replace(text, Mask);
        text = LongOpaque().Replace(text, m => IsRandom(m.Value) ? Mask : m.Value);
        return text;
    }

    [GeneratedRegex(@"(?<scheme>\b[a-z][a-z0-9+.\-]*://)[^/\s:@""'<>]+(?::[^/\s@""'<>]*)?@", RegexOptions.IgnoreCase)]
    private static partial Regex UrlUserInfo();

    [GeneratedRegex(@"(?<url>\b[a-z][a-z0-9+.\-]*://[^\s?#""'<>]*)\?[^\s#""'<>]*", RegexOptions.IgnoreCase)]
    private static partial Regex UrlQuery();

    [GeneratedRegex(@"(?<name>\b(?:(?:proxy-)?authorization|cookie|set-cookie)\s*[:=]\s*)[^\r\n]+",
        RegexOptions.IgnoreCase)]
    private static partial Regex AuthorizationHeader();

    [GeneratedRegex(@"(?<name>\b[A-Za-z0-9_\-]{0,64}(?:passw(?:or)?d|passphrase|pwd)\s*[:=]\s*)(?![""'])[^\r\n]+",
        RegexOptions.IgnoreCase)]
    private static partial Regex PassphraseLine();

    [GeneratedRegex(@"(?<scheme>\b(?:bearer|basic|digest|token))\s+[A-Za-z0-9\-._~+/]{8,}=*", RegexOptions.IgnoreCase)]
    private static partial Regex SchemeToken();

    [GeneratedRegex(
        @"(?<key>(?<flag>(?<![\w\-/])(?:--?|/))?[A-Za-z0-9_\-]{0,64}(?:passw(?:or)?d|passphrase|pwd|secret|token|api[_\-.]?key|access[_\-.]?key|private[_\-.]?key|credential|cookie|session[_\-]?id|signature|auth(?=[""']?\s*[:=]|\s))[A-Za-z0-9_\-]{0,64}[""']?)(?<sep>\s*[:=]\s*|(?(flag)\s+|(?!)))(?!\[redacted\])(?:""(?:[^""\\\r\n]|\\.)*""|'(?:[^'\\\r\n]|\\.)*'|[^\s,;&""'}\])]+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex Assignment();

    [GeneratedRegex(
        @"\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|xox[abprs]-[A-Za-z0-9\-]{10,}|sk-[A-Za-z0-9\-_]{16,}|AKIA[A-Z0-9]{16})\b")]
    private static partial Regex KnownTokenShape();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]*")]
    private static partial Regex Jwt();

    private static bool IsRandom(string run)
    {
        return run.Any(char.IsAsciiDigit) && run.Any(char.IsAsciiLetterUpper) && run.Any(char.IsAsciiLetterLower) &&
               run.Count(c => c == '-') <= 2;
    }

    [GeneratedRegex(@"[A-Za-z0-9_\-]{40,}")]
    private static partial Regex LongOpaque();
}
