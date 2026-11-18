#requires -Version 7.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:Kinds = @('command', 'service', 'widget', 'block')
$script:Sections = @('Sub-features', 'How to get to it (user POV)', 'Driving it with winshell inspect', 'Gotchas')
$script:WinshellKeys = @('winshell', 'exit', 'output', 'file', 'within', 'background', 'for')
$script:ElementKeys = @('element', 'window', 'has', 'within')

function Get-StepCommand($Step) {
    if ($Step.PSObject.Properties.Name -contains 'element') { return 'winshell inspect --pid {pid} tree' }
    $words = @($Step.winshell | ForEach-Object { if ([string] $_ -match '\s') { "`"$_`"" } else { [string] $_ } })
    'winshell ' + ($words -join ' ')
}

function Test-Regex([string] $Pattern) {
    try { $null = [regex]::new($Pattern); $true } catch { $false }
}

function Get-StepProblems($Step, [int] $Number) {
    if ($Step -isnot [Management.Automation.PSCustomObject]) { return @("step $Number is not an object.") }
    $keys = @($Step.PSObject.Properties.Name)
    $isWinshell = $keys -contains 'winshell'
    $isElement = $keys -contains 'element'
    if ($isWinshell -eq $isElement) { return @("step $Number needs exactly one of `"winshell`" and `"element`".") }
    $allowed = if ($isWinshell) { $script:WinshellKeys } else { $script:ElementKeys }
    $kind = if ($isWinshell) { 'winshell' } else { 'element' }
    foreach ($key in $keys | Where-Object { $allowed -notcontains $_ }) {
        "step $Number has `"$key`", which a $kind step does not take. It takes $($allowed -join ', ')."
    }
    if ($isWinshell -and (@($Step.winshell).Count -eq 0 -or @($Step.winshell | Where-Object { $_ -isnot [string] }).Count -gt 0)) {
        "step ${Number}: `"winshell`" is a list of the words after winshell, each a string."
    }
    if ($isElement -and ($Step.element -isnot [string] -or $Step.element.Length -eq 0)) {
        "step ${Number}: `"element`" is the x:Name, automation id or automation name to find."
    }
    foreach ($pattern in @(if ($keys -contains 'output') { $Step.output }) + @(if ($keys -contains 'has') { $Step.has.PSObject.Properties.Value })) {
        if ($pattern -isnot [string] -or -not (Test-Regex $pattern)) { "step ${Number}: $pattern is not a regular expression." }
    }
}

function Get-SectionLines([string[]] $Lines) {
    $sections = [ordered]@{}
    $current = $null
    $fenced = $false
    foreach ($line in $Lines) {
        if ($line.StartsWith('```')) { $fenced = -not $fenced }
        if (-not $fenced -and $line -match '^## (.+)$') {
            $current = $Matches[1]
            if (-not $sections.Contains($current)) { $sections[$current] = [Collections.Generic.List[string]]::new() }
            continue
        }
        if ($null -ne $current) { $sections[$current].Add($line) }
    }
    $sections
}

function Read-Feature([string] $Path) {
    $id = [IO.Path]::GetFileNameWithoutExtension($Path)
    $kind = ($id -split '\.')[0]
    $problems = [Collections.Generic.List[string]]::new()
    $lines = @(([IO.File]::ReadAllText($Path) -replace "`r`n", "`n").TrimEnd("`n") -split "`n")
    $feature = [pscustomobject]@{ id = $id; kind = $kind; title = ''; shell = $null; steps = @(); problems = $problems }

    if ($id -cnotmatch '^[a-z]+\.[a-z0-9]+(-[a-z0-9]+)*$' -or $script:Kinds -notcontains $kind) {
        $problems.Add("the file name is <kind>.<slug>.md, with a kind of $($script:Kinds -join ', ') and a slug in lowercase with hyphens.")
    }
    if (@($lines | Where-Object { $_ -cmatch '\bTODO\b' }).Count -gt 0) {
        $problems.Add('the file still says TODO. Write what the user sees instead.')
    }
    if ($lines[0] -notmatch '^# \S') {
        $problems.Add('line 1 is the H1 title, such as "# Audio".')
    }
    else {
        $feature.title = $lines[0].Substring(2)
    }
    if ($lines.Count -lt 3 -or $lines[1] -ne '' -or $lines[2] -match '^(#|-|```|$)') {
        $problems.Add('after the title and a blank line comes one paragraph that says what the user sees.')
    }
    $sections = Get-SectionLines $lines
    $headings = @($sections.Keys)
    if (($headings -join '|') -ne ($script:Sections -join '|')) {
        $expected = ($script:Sections | ForEach-Object { '"' + $_ + '"' }) -join ', '
        $actual = ($headings | ForEach-Object { '"' + $_ + '"' }) -join ', '
        $problems.Add("the H2 sections are exactly $expected, in that order. This file has $actual.")
    }
    if (@($lines | Select-Object -Skip 1 | Where-Object { $_ -match '^# ' }).Count -gt 0) {
        $problems.Add('only line 1 is an H1.')
    }
    if ($sections.Contains('Sub-features') -and @($sections['Sub-features'] | Where-Object { $_ -cmatch '^- `[a-z0-9]+(-[a-z0-9]+)*` \S' }).Count -eq 0) {
        $problems.Add('Sub-features lists each behavior as "- `short-id` what it does".')
    }
    foreach ($name in 'How to get to it (user POV)', 'Gotchas') {
        if ($sections.Contains($name) -and @($sections[$name] | Where-Object { $_ -match '^- \S' }).Count -eq 0) {
            $problems.Add("$name has at least one bullet.")
        }
    }
    $driving = 'Driving it with winshell inspect'
    if (-not $sections.Contains($driving)) { return $feature }
    $body = @($sections[$driving])
    if ($body -notcontains 'Preconditions:') { $problems.Add("$driving starts with a `"Preconditions:`" line.") }
    $shells = @($body | Where-Object { $_ -match '^- Shell: `([^`]+)`' } | ForEach-Object { [regex]::Match($_, '^- Shell: `([^`]+)`').Groups[1].Value })
    if ($shells.Count -ne 1 -or $script:Shells.Keys -notcontains $shells[0]) {
        $problems.Add("Preconditions has one `"- Shell: ``<name>``.`" bullet, where the name is one of $($script:Shells.Keys -join ', ').")
    }
    else {
        $feature.shell = $shells[0]
    }
    $bullets = @($body | Where-Object { $_ -match '^- \*\*[^*]+\*\* \S' })
    $start = [Array]::IndexOf($body, '```json')
    $end = if ($start -ge 0) { [Array]::IndexOf($body, '```', $start + 1) } else { -1 }
    if ($start -lt 0 -or $end -lt 0 -or @($body | Where-Object { $_ -eq '```json' }).Count -ne 1) {
        $problems.Add("$driving ends with one ``````json block that holds the steps the driver runs.")
        return $feature
    }
    try {
        $steps = @(($body[($start + 1)..($end - 1)] -join "`n") | ConvertFrom-Json -Depth 16 -NoEnumerate)
        if ($steps.Count -eq 1 -and $steps[0] -is [array]) { $steps = @($steps[0]) }
    }
    catch {
        $problems.Add("the ``````json block is not JSON: $($_.Exception.Message)")
        return $feature
    }
    if ($steps.Count -eq 0) { $problems.Add('the ```json block lists at least one step.') }
    for ($i = 0; $i -lt $steps.Count; $i++) {
        foreach ($problem in Get-StepProblems $steps[$i] ($i + 1)) { $problems.Add($problem) }
    }
    if ($problems.Count -eq 0) {
        if ($bullets.Count -ne $steps.Count) {
            $problems.Add("$driving has $($bullets.Count) step bullets, `"- **Label.** ...`", and $($steps.Count) steps. Write one bullet for each step, in order.")
        }
        else {
            for ($i = 0; $i -lt $steps.Count; $i++) {
                $command = Get-StepCommand $steps[$i]
                if (-not $bullets[$i].Contains("``$command``")) {
                    $problems.Add("step bullet $($i + 1) names its command exactly: ``$command``.")
                }
            }
        }
        if ($feature.shell -eq 'features' -and $kind -in 'service', 'block' -and
            @($steps | Where-Object { (Get-Field $_ 'element') -ceq $id }).Count -eq 0) {
            $problems.Add("on the features shell, a step finds the element $id. Give the feature shell's row that automation id.")
        }
    }
    $feature.steps = $steps
    $feature
}

function Read-FeatureMap([string] $Folder) {
    @(Get-ChildItem -LiteralPath $Folder -Filter '*.md' | Where-Object Name -NE 'README.md' | Sort-Object Name |
            ForEach-Object { Read-Feature $_.FullName })
}

function Get-IndexProblems([string] $Folder, $Features) {
    $readme = Join-Path $Folder 'README.md'
    if (-not (Test-Path -LiteralPath $readme)) { return @('README.md: the index is missing.') }
    $sections = Get-SectionLines @((Get-Content -LiteralPath $readme) -replace "`r", '')
    if (-not $sections.Contains('Features')) { return @('README.md: a "## Features" section lists every feature file.') }
    $linked = @($sections['Features'] | ForEach-Object { [regex]::Matches($_, '\]\(\./([^)]+)\.md\)') } | ForEach-Object { $_.Groups[1].Value })
    $ids = @($Features | ForEach-Object id)
    foreach ($id in $ids | Where-Object { $linked -notcontains $_ }) { "README.md: Features does not link ./$id.md. Add a line for it." }
    foreach ($id in $linked | Where-Object { $ids -notcontains $_ }) { "README.md: Features links ./$id.md, which is not a feature file. Remove the line." }
    foreach ($group in $linked | Group-Object | Where-Object Count -GT 1) { "README.md: Features links ./$($group.Name).md $($group.Count) times. Keep one." }
}
