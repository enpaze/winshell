#requires -Version 7.0
[CmdletBinding()]
param(
    [switch] $VerifyNoChanges,
    [string] $Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$directive = '^(#requires\b|#\s*zizmor:|#\s*yaml-language-server:)'
$xmlFiles = '\.(csproj|props|targets|xml|manifest|config|slnx)$'

function Get-LineSpans([string] $Text, [string] $Pattern) {
    foreach ($match in [regex]::Matches($Text, $Pattern, 'Multiline')) {
        [pscustomobject]@{ Start = $match.Index; Length = $match.Length }
    }
}

function Get-PowerShellSpans([string] $Path) {
    $tokens = $null
    $parseErrors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors) { throw "Could not parse ${Path}: $($parseErrors[0])" }
    foreach ($token in $tokens) {
        if ($token.Kind -eq 'Comment' -and $token.Text -notmatch $directive) {
            [pscustomobject]@{ Start = $token.Extent.StartOffset; Length = $token.Extent.EndOffset - $token.Extent.StartOffset }
        }
    }
}

function Get-TrailingHash([string] $Line) {
    $quote = $null
    for ($i = 0; $i -lt $Line.Length; $i++) {
        $c = $Line[$i]
        if ($quote) {
            if ($c -eq $quote) { $quote = $null }
        }
        elseif ($c -eq '"' -or $c -eq "'") { $quote = $c }
        elseif ($c -eq '#' -and ($i -eq 0 -or [char]::IsWhiteSpace($Line[$i - 1]))) { return $i }
    }
    -1
}

function Get-YamlSpans([string] $Text) {
    $offset = 0
    $block = $null
    foreach ($line in $Text.Split("`n")) {
        $indent = $line.Length - $line.TrimStart().Length
        if ($block -and $line.Trim().Length -gt 0 -and $indent -le $block.Indent) { $block = $null }
        if (-not $block -or $block.Code) {
            $hash = Get-TrailingHash $line
            if ($hash -ge 0 -and $line.Substring($hash) -notmatch $directive) {
                [pscustomobject]@{ Start = $offset + $hash; Length = $line.TrimEnd("`r").Length - $hash }
            }
        }
        if (-not $block -and $line -match '^(\s*)(?:-\s+)?([\w.-]+):\s*[|>][-+0-9]*\s*(?:#.*)?$') {
            $block = @{ Indent = $Matches[1].Length; Code = $Matches[2] -eq 'run' }
        }
        $offset += $line.Length + 1
    }
}

function Get-CSharpSpans([string[]] $Paths) {
    if ($Paths.Count -eq 0) { return }
    $lines = $Paths | dotnet run --file (Join-Path $PSScriptRoot 'comments.cs')
    if ($LASTEXITCODE -ne 0) { throw 'The C# comment finder failed.' }
    foreach ($line in $lines) {
        $path, $start, $length = $line.Split("`t")
        [pscustomobject]@{ Path = $path; Start = [int]$start; Length = [int]$length }
    }
}

function Test-Blank([string] $Line) { $Line.Trim().Length -eq 0 }

function Remove-Spans([string] $Text, [object[]] $Spans) {
    $removed = [bool[]]::new($Text.Length)
    foreach ($span in $Spans) {
        for ($i = $span.Start; $i -lt $span.Start + $span.Length; $i++) { $removed[$i] = $true }
    }
    $lines = [Collections.Generic.List[object]]::new()
    $start = 0
    while ($start -lt $Text.Length) {
        $end = $Text.IndexOf("`n", $start)
        $next = if ($end -lt 0) { $Text.Length } else { $end + 1 }
        $contentEnd = if ($end -lt 0) { $Text.Length } else { $end }
        if ($contentEnd -gt $start -and $Text[$contentEnd - 1] -eq "`r") { $contentEnd-- }
        $kept = [Text.StringBuilder]::new()
        $touched = $false
        for ($i = $start; $i -lt $contentEnd; $i++) {
            if ($removed[$i]) {
                if (-not $touched -or -not $removed[$i - 1]) {
                    $trim = $kept.Length
                    while ($trim -gt 0 -and [char]::IsWhiteSpace($kept[$trim - 1])) { $trim-- }
                    if ($trim -gt 0) { [void]$kept.Remove($trim, $kept.Length - $trim) }
                }
                $touched = $true
            }
            else { [void]$kept.Append($Text[$i]) }
        }
        $content = $kept.ToString()
        $lines.Add([pscustomobject]@{
                Text = if ($touched) { $content.TrimEnd() } else { $content }
                Ending = $Text.Substring($contentEnd, $next - $contentEnd)
                Deleted = $touched -and (Test-Blank $content)
                Group = $touched -and (Test-Blank $content)
            })
        $start = $next
    }
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if (-not $lines[$i].Group -or ($i + 1 -lt $lines.Count -and $lines[$i + 1].Group)) { continue }
        $first = $i
        while ($first -gt 0 -and $lines[$first - 1].Group) { $first-- }
        $prev = $first - 1
        while ($prev -ge 0 -and $lines[$prev].Deleted) { $prev-- }
        $next = $i + 1
        while ($next -lt $lines.Count -and $lines[$next].Deleted) { $next++ }
        $prevLine = if ($prev -ge 0) { $lines[$prev] } else { $null }
        $nextLine = if ($next -lt $lines.Count) { $lines[$next] } else { $null }
        $prevBlank = $prevLine -and (Test-Blank $prevLine.Text)
        $nextBlank = $nextLine -and (Test-Blank $nextLine.Text)
        $opens = $prevLine -and ($prevLine.Text -match '[{(\[]\s*$|^\s*<[^/!?][^>]*[^/]>\s*$')
        $closes = $nextLine -and ($nextLine.Text -match '^\s*([})\]]|</)')
        if ($nextBlank -and ($prevBlank -or -not $prevLine -or $opens)) { $nextLine.Deleted = $true }
        elseif ($prevBlank -and ($closes -or -not $nextLine)) { $prevLine.Deleted = $true }
    }
    -join ($lines | Where-Object { -not $_.Deleted } | ForEach-Object { $_.Text + $_.Ending })
}

Push-Location $Root
try {
    $paths = @(git -c core.quotepath=false ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Could not list files with Git.' }
    $paths = @($paths | Where-Object { [IO.File]::Exists((Join-Path $Root $_)) })
    $spans = @{}
    foreach ($span in Get-CSharpSpans @($paths | Where-Object { $_ -match '\.(cs|md)$' })) {
        if (-not $spans[$span.Path]) { $spans[$span.Path] = [Collections.Generic.List[object]]::new() }
        $spans[$span.Path].Add($span)
    }
    foreach ($path in $paths) {
        $full = Join-Path $Root $path
        $found = switch -Regex ($path) {
            '\.ps(m|d)?1$' { Get-PowerShellSpans $full }
            '\.ya?ml$' { Get-YamlSpans ([IO.File]::ReadAllText($full)) }
            $xmlFiles { Get-LineSpans ([IO.File]::ReadAllText($full)) '<!--[\s\S]*?-->' }
            '(^|/)\.editorconfig$' { Get-LineSpans ([IO.File]::ReadAllText($full)) '^[ \t]*[#;].*?(?=\r?$)' }
            '(^|/)\.git(ignore|attributes)$' { Get-LineSpans ([IO.File]::ReadAllText($full)) '^[ \t]*#.*?(?=\r?$)' }
        }
        if ($found) { $spans[$path] = @($found) }
    }

    $count = 0
    foreach ($path in @($spans.Keys | Sort-Object)) {
        $full = Join-Path $Root $path
        $text = [IO.File]::ReadAllText($full)
        if ($VerifyNoChanges) {
            foreach ($span in $spans[$path] | Sort-Object Start) {
                $line = $text.Substring(0, $span.Start).Split("`n").Count
                $comment = $text.Substring($span.Start, $span.Length).Trim().Split("`n")[0].Trim()
                Write-Output "${path}:${line}: $comment"
                $count++
            }
            continue
        }
        $bom = [IO.File]::ReadAllBytes($full)[0] -eq 0xEF
        [IO.File]::WriteAllText($full, (Remove-Spans $text @($spans[$path])), [Text.UTF8Encoding]::new($bom))
        $count += $spans[$path].Count
    }
    if ($VerifyNoChanges -and $count -gt 0) {
        Write-Output "$count comments. Remove them with: pwsh scripts/comments.ps1"
        exit 1
    }
    if (-not $VerifyNoChanges) { Write-Output "Removed $count comments from $($spans.Count) files." }
}
finally {
    Pop-Location
}
