using System.Globalization;
using System.Text;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace Winshell;

internal sealed partial class TomlDocument
{
    private const int MaxTextBytes = 1024 * 1024;

    private static string? FileBytes(long bytes)
    {
        return bytes > MaxTextBytes
            ? string.Create(CultureInfo.InvariantCulture, $"file is {bytes} bytes, above the limit of {MaxTextBytes}")
            : null;
    }

    internal static string Problem(string where, string message, int line, int column)
    {
        return line > 0 ? $"{At(where, line, column)}: {message}" : $"{where}: {message}";
    }

    private static string At(string where, int line, int column)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{where}[{line}:{column}]");
    }

    private static (string Message, int Line, int Column) Locate(DiagnosticMessage diagnostic)
    {
        return (diagnostic.Message, diagnostic.Span.Start.Line + 1, diagnostic.Span.Start.Column + 1);
    }

    private static string KeyAt(string where, DocumentSyntax document, string key)
    {
        var owned = FindOwned(document, key);
        SyntaxNode? first = owned.KeyValues.Count > 0 ? owned.KeyValues[0] : owned.Tables.FirstOrDefault();
        if (first?.Span.Start is not { } start)
            return where;
        return At(where, start.Line + 1, start.Column + 1);
    }

    private static string KeyAt(string where, DocumentSyntax document, string table, string key)
    {
        foreach (var header in FindOwned(document, table).Tables)
            if (header is TableSyntax && IsSimple(header.Name) && FindItem(header.Items, key) is
                { Span.Start: var start })
                return At(where, start.Line + 1, start.Column + 1);
        return KeyAt(where, document, table);
    }

    private static bool TryLoad(string text, out DocumentSyntax document, out TomlTable table, out string message,
        out int line, out int column)
    {
        document = new DocumentSyntax();
        table = [];
        line = 0;
        column = 0;
        if (FileBytes(Encoding.UTF8.GetByteCount(text)) is { } why)
        {
            message = why;
            return false;
        }

        try
        {
            document = SyntaxParser.Parse(text);
        }
        catch (TomlException e)
        {
            message = e.Message;
            return false;
        }

        if (document.HasErrors)
        {
            if (document.Diagnostics.Count > 0)
                (message, line, column) = Locate(document.Diagnostics[0]);
            else
                message = "invalid TOML";
            return false;
        }

        try
        {
            table = TomlSerializer.Deserialize<TomlTable>(text) ?? [];
            message = "";
            return true;
        }
        catch (TomlException e)
        {
            message = e.Message;
            if (e.Line is { } at and > 0)
            {
                line = at;
                column = e.Column ?? 0;
            }

            return false;
        }
    }

    private static void RemoveInline(InlineTableSyntax table, InlineTableItemSyntax item)
    {
        var index = 0;
        while (table.Items.GetChild(index) != item)
            index++;
        table.Items.RemoveChild(item);
        if (index > 0 && index == table.Items.ChildrenCount)
            table.Items.GetChild(index - 1)!.Comma = null;
    }

    private static void RemoveLastItem(ArraySyntax array)
    {
        var items = array.Items;
        var last = items.GetChild(items.ChildrenCount - 1)!;
        var end = After(last) ?? [];
        items.RemoveChildAt(items.ChildrenCount - 1);
        if (items.ChildrenCount == 0)
        {
            var left = Ended(array.OpenBracket!.TrailingTrivia, end);
            array.OpenBracket.TrailingTrivia = left.Exists(t => t.Kind == TokenKind.Comment) ? left : null;
            return;
        }

        var kept = items.GetChild(items.ChildrenCount - 1)!;
        var comma = kept.Comma!;
        if (last.Comma is null)
        {
            var value = LastToken(kept.Value!);
            kept.Comma = null;
            value.TrailingTrivia = Ended([.. value.TrailingTrivia ?? [], .. comma.TrailingTrivia ?? []], end);
        }
        else
        {
            comma.TrailingTrivia = Ended(comma.TrailingTrivia, end);
        }
    }

    private static void AppendItem(ArraySyntax array, ValueSyntax value)
    {
        var items = array.Items;
        var item = new ArrayItemSyntax { Value = value };
        if (items.ChildrenCount == 0)
        {
            var open = array.OpenBracket!.TrailingTrivia;
            if (open is not null && LastBreak(open) is var start and >= 0)
                LastToken(value).TrailingTrivia = open[start..];
            items.Add(item);
            return;
        }

        var last = items.GetChild(items.ChildrenCount - 1)!;
        var before = items.ChildrenCount > 1
            ? items.GetChild(items.ChildrenCount - 2)!.Comma?.TrailingTrivia
            : array.OpenBracket!.TrailingTrivia;
        var end = After(last) ?? [];
        List<SyntaxTrivia> between;
        List<SyntaxTrivia> after;
        if (before is not null && LastBreak(before) is var line and >= 0)
        {
            var endLine = LastBreak(end);
            between = endLine >= 0 ? [.. end[..(endLine + 1)], .. before[(line + 1)..]] : before[line..];
            after = endLine >= 0 ? end[endLine..] : end;
        }
        else
        {
            between = before is { Count: > 0 } ? [.. before] : [new SyntaxTrivia(TokenKind.Whitespaces, " ")];
            after = end;
        }

        if (last.Comma is null)
        {
            LastToken(last.Value!).TrailingTrivia = null;
            LastToken(value).TrailingTrivia = after;
        }
        else
        {
            item.Comma = Comma(after);
        }

        last.Comma = Comma(between);
        items.Add(item);
    }

    private static List<SyntaxTrivia>? After(ArrayItemSyntax item)
    {
        return item.Comma is { } comma ? comma.TrailingTrivia : LastToken(item.Value!).TrailingTrivia;
    }

    private static List<SyntaxTrivia> Ended(List<SyntaxTrivia>? own, List<SyntaxTrivia> end)
    {
        var endLine = end.FindIndex(t => t.Kind == TokenKind.NewLine);
        var ownLine = own?.FindIndex(t => t.Kind == TokenKind.NewLine) ?? -1;
        if (own is null || ownLine < 0)
            return end[Math.Max(endLine, 0)..];
        var lines = own[..(ownLine + 1)];
        if (endLine >= 0)
            return [.. lines, .. end[(endLine + 1)..]];
        return lines.Exists(t => t.Kind == TokenKind.Comment) ? lines : end;
    }

    private static int LastBreak(List<SyntaxTrivia> trivia)
    {
        return trivia.FindLastIndex(t => t.Kind == TokenKind.NewLine);
    }

    private static void AppendRaw(DocumentSyntax document, string keyText, string tomlValue, string newline,
        IEnumerable<string>? commentLines = null)
    {
        var fresh = ParseAssignment(keyText, tomlValue, newline);
        var leading = new List<SyntaxTrivia>();
        if (document.KeyValues.ChildrenCount == 0)
            leading.AddRange(StealPreamble(document));
        else
            leading.Add(new SyntaxTrivia(TokenKind.NewLine, newline));
        if (commentLines is not null)
            foreach (var line in commentLines)
            {
                leading.Add(new SyntaxTrivia(TokenKind.Comment, line));
                leading.Add(new SyntaxTrivia(TokenKind.NewLine, newline));
            }

        FirstToken(fresh.Key!).LeadingTrivia = leading;
        Append(document, fresh, newline);
    }

    private static List<SyntaxTrivia> StealPreamble(DocumentSyntax document)
    {
        if (document.TrailingTrivia is { Count: > 0 } trailing)
        {
            document.TrailingTrivia = null;
            return trailing;
        }

        if (document.Tables.ChildrenCount > 0 &&
            document.Tables.GetChild(0) is { LeadingTrivia: { Count: > 0 } leading } table)
        {
            table.LeadingTrivia = null;
            return leading;
        }

        return [];
    }

    private static void Append(DocumentSyntax document, KeyValueSyntax fresh, string newline)
    {
        List<SyntaxTrivia>? after = document.Tables.ChildrenCount > 0
            ? [new SyntaxTrivia(TokenKind.NewLine, newline)]
            : null;
        var count = document.KeyValues.ChildrenCount;
        if (count > 0)
        {
            var last = document.KeyValues.GetChild(count - 1)!;
            last.EndOfLineToken ??= new SyntaxToken(TokenKind.NewLine, newline);
            after = last.EndOfLineToken.TrailingTrivia;
            last.EndOfLineToken.TrailingTrivia = null;
        }

        document.KeyValues.Add(fresh);
        fresh.EndOfLineToken ??= new SyntaxToken(TokenKind.NewLine, newline);
        fresh.EndOfLineToken.TrailingTrivia = after;
    }

    private static void AddKey(DocumentSyntax document, TableSyntaxBase table, KeyValueSyntax fresh, string newline)
    {
        var items = table.Items;
        var end = items.ChildrenCount > 0
            ? items.GetChild(items.ChildrenCount - 1)!.EndOfLineToken ??= new SyntaxToken(TokenKind.NewLine, newline)
            : table.EndOfLineToken ??= new SyntaxToken(TokenKind.NewLine, newline);
        items.Add(fresh);
        if (ReferenceEquals(document.Tables.GetChild(document.Tables.ChildrenCount - 1), table) ||
            end.TrailingTrivia is not { } after)
            return;
        var split = NextStarts(after);
        fresh.EndOfLineToken ??= new SyntaxToken(TokenKind.NewLine, newline);
        fresh.EndOfLineToken.TrailingTrivia = after[split..];
        end.TrailingTrivia = after[..split];
    }

    private static int NextStarts(List<SyntaxTrivia> trivia)
    {
        List<int> starts = [0];
        for (var i = 0; i < trivia.Count; i++)
            if (trivia[i].Kind == TokenKind.NewLine)
                starts.Add(i + 1);
        for (var line = starts.Count - 2; line >= 0; line--)
            if (!trivia[starts[line]..starts[line + 1]].Exists(t => t.Kind == TokenKind.Comment))
                return starts[line];
        return 0;
    }

    private static KeyValueSyntax ParseAssignment(string keyText, string tomlValue, string newline)
    {
        var parsed = SyntaxParser.Parse(keyText + " = " + tomlValue + newline);
        var fresh = parsed.KeyValues.GetChild(0)!;
        parsed.KeyValues.RemoveChild(fresh);
        return fresh;
    }

    private static List<SyntaxTrivia>? Remove(SyntaxList<KeyValueSyntax> list, KeyValueSyntax kv, string newline)
    {
        var above = kv.LeadingTrivia;
        List<SyntaxTrivia> kept =
        [
            .. above is not null && above.Exists(t => t.Kind == TokenKind.Comment) ? above : [],
            .. kv.EndOfLineToken?.TrailingTrivia ?? []
        ];
        var index = 0;
        while (list.GetChild(index) != kv)
            index++;
        list.RemoveChild(kv);
        if (kept.Count == 0)
            return null;
        if (index > 0)
        {
            var previous = list.GetChild(index - 1)!;
            previous.EndOfLineToken ??= new SyntaxToken(TokenKind.NewLine, newline);
            previous.EndOfLineToken.TrailingTrivia = [.. previous.EndOfLineToken.TrailingTrivia ?? [], .. kept];
            return null;
        }

        if (index < list.ChildrenCount)
        {
            var next = list.GetChild(index)!;
            next.LeadingTrivia = [.. kept, .. next.LeadingTrivia ?? []];
            return null;
        }

        return kept;
    }

    private static void RemoveKey(DocumentSyntax document, KeyValueSyntax kv, string newline)
    {
        if (Remove(document.KeyValues, kv, newline) is not { } kept)
            return;
        if (document.Tables.ChildrenCount > 0)
        {
            var first = document.Tables.GetChild(0)!;
            first.LeadingTrivia = [.. kept, .. first.LeadingTrivia ?? []];
        }
        else
        {
            document.TrailingTrivia = [.. kept, .. document.TrailingTrivia ?? []];
        }
    }

    private static void RemoveTable(DocumentSyntax document, TableSyntaxBase table)
    {
        var index = 0;
        while (document.Tables.GetChild(index) != table)
            index++;
        var after = LastToken(table).TrailingTrivia ?? [];
        var comment = after.FindIndex(t => t.Kind == TokenKind.Comment);
        if (comment >= 0 && EndsBlank(table.LeadingTrivia ?? Above(document, index)))
            after = after[(after.FindLastIndex(comment, t => t.Kind == TokenKind.NewLine) + 1)..];
        List<SyntaxTrivia> kept = [.. table.LeadingTrivia ?? [], .. comment >= 0 ? after : []];
        document.Tables.RemoveChild(table);
        if (kept.Count == 0)
            return;
        if (index < document.Tables.ChildrenCount)
        {
            var next = document.Tables.GetChild(index)!;
            next.LeadingTrivia = [.. kept, .. next.LeadingTrivia ?? []];
        }
        else if (index > 0)
        {
            var previous = LastToken(document.Tables.GetChild(index - 1)!);
            previous.TrailingTrivia = [.. previous.TrailingTrivia ?? [], .. kept];
        }
        else if (document.KeyValues.ChildrenCount > 0)
        {
            var last = document.KeyValues.GetChild(document.KeyValues.ChildrenCount - 1)!;
            if (last.EndOfLineToken is not null)
                last.EndOfLineToken.TrailingTrivia = [.. last.EndOfLineToken.TrailingTrivia ?? [], .. kept];
        }
        else
        {
            document.TrailingTrivia = [.. kept, .. document.TrailingTrivia ?? []];
        }
    }

    private static List<SyntaxTrivia>? Above(DocumentSyntax document, int index)
    {
        if (index > 0)
            return LastToken(document.Tables.GetChild(index - 1)!).TrailingTrivia ?? [];
        if (document.KeyValues.ChildrenCount > 0)
            return LastToken(document.KeyValues.GetChild(document.KeyValues.ChildrenCount - 1)!).TrailingTrivia ?? [];
        return null;
    }

    private static bool EndsBlank(List<SyntaxTrivia>? trivia)
    {
        if (trivia is null)
            return true;
        var kinds = trivia.Where(t => t.Kind != TokenKind.Whitespaces).Select(t => t.Kind).ToList();
        return kinds.Count > 0 && kinds[^1] == TokenKind.NewLine &&
               (kinds.Count == 1 || kinds[^2] == TokenKind.NewLine);
    }

    private static void InsertAfter(DocumentSyntax document, TableSyntaxBase after, TableSyntaxBase table)
    {
        var following = new List<TableSyntaxBase>();
        var seen = false;
        foreach (var current in document.Tables)
            if (seen)
                following.Add(current);
            else if (ReferenceEquals(current, after))
                seen = true;
        foreach (var current in following)
            document.Tables.RemoveChild(current);
        document.Tables.Add(table);
        foreach (var current in following)
            document.Tables.Add(current);
    }

    private static Owned FindOwned(DocumentSyntax document, string name)
    {
        var owned = new Owned();
        foreach (var kv in document.KeyValues)
            if (RootName(kv.Key) == name)
                owned.KeyValues.Add(kv);
        foreach (var table in document.Tables)
            if (RootName(table.Name) == name)
                owned.Tables.Add(table);
        return owned;
    }

    private static KeyValueSyntax? FindItem(SyntaxList<KeyValueSyntax> items, string name)
    {
        foreach (var kv in items)
            if (Names(kv.Key, name))
                return kv;
        return null;
    }

    private static bool Names(KeySyntax? key, string name)
    {
        return IsSimple(key) && RootName(key) == name;
    }

    private static bool IsSimple(KeySyntax? key)
    {
        return key is not null && key.DotKeys.ChildrenCount == 0;
    }

    private static string? RootName(KeySyntax? key)
    {
        return key is null ? null : SegmentText(key.Key);
    }

    private static IEnumerable<string> Segments(KeySyntax key)
    {
        if (SegmentText(key.Key) is { } first)
            yield return first;
        foreach (var dot in key.DotKeys)
            if (SegmentText(dot.Key) is { } part)
                yield return part;
    }

    private static string? SegmentText(BareKeyOrStringValueSyntax? key)
    {
        return key switch
        {
            BareKeySyntax bare => bare.Key?.Text,
            StringValueSyntax quoted => quoted.Value,
            _ => null
        };
    }

    private static string NewlineOf(DocumentSyntax document)
    {
        foreach (var kv in document.KeyValues)
            if (Line(kv.EndOfLineToken) is { } line)
                return line;
        foreach (var table in document.Tables)
        {
            if (Line(table.EndOfLineToken) is { } line)
                return line;
            foreach (var kv in table.Items)
                if (Line(kv.EndOfLineToken) is { } nested)
                    return nested;
        }

        if (TriviaNewline(document.TrailingTrivia) is { } trailing)
            return trailing;
        return "\n";
    }

    private static string? TriviaNewline(List<SyntaxTrivia>? trivia)
    {
        if (trivia is null)
            return null;
        foreach (var item in trivia)
            if (item is { Kind: TokenKind.NewLine, Text: { Length: > 0 } text })
                return text == "\r\n" ? "\r\n" : "\n";
        return null;
    }

    private static string? Line(SyntaxToken? token)
    {
        return token is { TokenKind: TokenKind.NewLine, Text: { Length: > 0 } text }
            ? text == "\r\n" ? "\r\n" : "\n"
            : null;
    }

    private static SyntaxToken FirstToken(SyntaxNode node)
    {
        return node.Tokens(false).OfType<SyntaxToken>().First();
    }

    private static SyntaxToken LastToken(SyntaxNode node)
    {
        return node.Tokens(false).OfType<SyntaxToken>().Last();
    }

    private static SyntaxToken Comma()
    {
        return Comma([new SyntaxTrivia(TokenKind.Whitespaces, " ")]);
    }

    private static SyntaxToken Comma(List<SyntaxTrivia> trailing)
    {
        return new SyntaxToken(TokenKind.Comma, ",") { TrailingTrivia = trailing };
    }

    private static void SetString(StringValueSyntax node, string text)
    {
        node.Value = text;
        node.Token!.TokenKind = TokenKind.String;
        node.Token.Text = Quote(text);
    }

    private static void SetBool(BooleanValueSyntax node, bool value)
    {
        node.Value = value;
        node.Token!.TokenKind = value ? TokenKind.True : TokenKind.False;
        node.Token.Text = value ? "true" : "false";
    }

    public static string Key(string key)
    {
        return key.Length > 0 && key.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') ? key : Quote(key);
    }

    private static string Quote(string text)
    {
        var quoted = new StringBuilder("\"");
        foreach (var c in text)
            quoted.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ when char.IsControl(c) => $"\\u{(int)c:X4}",
                _ => c.ToString()
            });
        return quoted.Append('"').ToString();
    }

    private sealed class Owned
    {
        public List<KeyValueSyntax> KeyValues { get; } = [];

        public List<TableSyntaxBase> Tables { get; } = [];

        public bool Defined => KeyValues.Count > 0 || Tables.Count > 0;
    }
}
