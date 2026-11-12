# A # ends a comment only outside quotes. "Fixed by #$PR." is still the command.
function script:Remove-TrailingComment([string] $Line) {
    $quote = [char]0
    for ($i = 0; $i -lt $Line.Length; $i++) {
        $c = $Line[$i]
        if ($quote -ne [char]0) {
            if ($c -eq $quote) { $quote = [char]0 }
            continue
        }
        if ($c -eq '"' -or $c -eq "'") { $quote = $c; continue }
        if ($c -eq '#' -and ($i -eq 0 -or [char]::IsWhiteSpace($Line[$i - 1]))) {
            return $Line.Substring(0, $i).TrimEnd()
        }
    }
    return $Line.TrimEnd()
}

# A > fold is one value. A | block is not: those lines are separate commands.
function script:Join-FoldedScalars($Lines) {
    $joined = [System.Collections.Generic.List[string]]::new()
    $foldIndent = -1
    $buffer = ''
    foreach ($line in $Lines) {
        if ($foldIndent -ge 0) {
            $indent = $line.Length - $line.TrimStart().Length
            if ($indent -gt $foldIndent) {
                $buffer += ' ' + $line.Trim()
                continue
            }
            $joined.Add($buffer.TrimEnd())
            $foldIndent = -1
        }
        if ($line -match '^(?<indent>\s*)(?<key>\S.*:\s*)>[+-]?\s*$') {
            $foldIndent = $Matches.indent.Length
            $buffer = $Matches.indent + $Matches.key.TrimEnd()
            continue
        }
        $joined.Add($line)
    }
    if ($foldIndent -ge 0) { $joined.Add($buffer.TrimEnd()) }
    return $joined
}

# Lines a workflow executes. A comment can repeat a command without running it, and a continued line is one command.
function script:Get-WorkflowCodeLines([string] $Text) {
    $kept = foreach ($line in ($Text -split '\r?\n')) {
        if ($line.Trim().StartsWith('#')) { continue }
        $code = Remove-TrailingComment $line
        if ($code.Trim() -eq '') { continue }
        $code
    }
    $kept = Join-FoldedScalars $kept
    $joined = [System.Collections.Generic.List[string]]::new()
    $buffer = ''
    foreach ($line in $kept) {
        # Bash continues with a backslash. PowerShell, the default shell on the Windows runners, continues with a backtick.
        if ($line.TrimEnd() -match '[\\`]$') {
            $piece = $line.TrimEnd() -replace '[\\`]$', ''
            $buffer = $(if ($buffer) { $buffer + $piece.Trim() } else { $piece.TrimEnd() }) + ' '
        }
        else {
            $joined.Add($(if ($buffer) { $buffer + $line.Trim() } else { $line.TrimEnd() }))
            $buffer = ''
        }
    }
    if ($buffer) { $joined.Add($buffer.TrimEnd()) }
    return $joined
}

# Each job's runs-on. A ${{ matrix.name }} value is the matrix entries for that name. An expression this cannot
# resolve is not a pinned image, so the job has no runner.
function script:Get-WorkflowJobs([string] $Text) {
    $jobs = [System.Collections.Generic.List[object]]::new()
    $inJobs = $false
    $name = $null
    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in (Get-WorkflowCodeLines $Text)) {
        if (-not $inJobs) {
            if ($line -match '^jobs:\s*$') { $inJobs = $true }
            continue
        }
        if ($line -match '^\S') { break }
        if ($line -match '^  ([A-Za-z0-9_-]+):\s*$') {
            if ($name) { $jobs.Add((New-WorkflowJob $name $lines)) }
            $name = $Matches[1]
            $lines = [System.Collections.Generic.List[string]]::new()
            continue
        }
        if (-not $name) { continue }
        $lines.Add($line)
    }
    if ($name) { $jobs.Add((New-WorkflowJob $name $lines)) }
    return $jobs
}

function script:New-WorkflowJob([string] $Name, $Lines) {
    # A reusable workflow call has no runner of its own. The called workflow's jobs do.
    $callsWorkflow = $false
    $runners = [System.Collections.Generic.List[string]]::new()
    $runsOn = $null
    $block = $null
    foreach ($line in $Lines) {
        if ($block -eq 'folded') {
            if ($line -match '^\s{6,}(?<value>\S.*)$') {
                $runners.Add($Matches.value.Trim().Trim('"').Trim("'"))
                continue
            }
            $block = $null
        }
        if ($line -match '^    uses:\s*\S') { $callsWorkflow = $true }
        if ($line -match '^    runs-on:\s*(?<value>.*)$') {
            $value = $Matches.value.Trim().Trim('"').Trim("'")
            # >- and | put the image on the next line. The indicator is not an image.
            if ($value -match '^[>|]') { $block = 'folded'; continue }
            $runsOn = $value
        }
    }
    if ($runsOn -match '^\$\{\{\s*matrix\.(?<key>[A-Za-z0-9_-]+)\s*\}\}$') {
        $key = [regex]::Escape($Matches.key)
        foreach ($line in $Lines) {
            if ($line -match "^\s*$key`:\s*(?<value>\S+)\s*$") { $runners.Add($Matches.value.Trim('"').Trim("'")) }
        }
    }
    elseif ($runsOn -and $runsOn -notmatch '\$\{\{') {
        $runners.Add($runsOn)
    }
    [pscustomobject]@{ Name = $Name; Runners = $runners; CallsWorkflow = $callsWorkflow }
}

# Arguments of each `git <subcommand>` in a line, up to the next command separator.
function script:Get-GitArguments([string] $Line, [string] $Subcommand) {
    $found = [System.Collections.Generic.List[object]]::new()
    foreach ($match in [regex]::Matches($Line, "git\s+$([regex]::Escape($Subcommand))\b([^;&|]*)")) {
        $tokens = @($match.Groups[1].Value -split '\s+' | Where-Object { $_ })
        $found.Add($tokens)
    }
    # The comma keeps one command a list of token lists. PowerShell would otherwise unwrap that one list.
    return ,$found.ToArray()
}

function script:Assert-GitTokens($Tokens, [string[]] $Expected) {
    $actual = @($Tokens | ForEach-Object { $_.Trim('"').Trim("'") })
    $actual.Count | Should -Be $Expected.Count
    for ($i = 0; $i -lt $Expected.Count; $i++) {
        $actual[$i] | Should -Be $Expected[$i]
    }
}

Describe 'The workflows' {
    BeforeAll {
        $script:Workflows = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '../../.github/workflows') -File
        $script:SkillPaths = @('.agents/skills', '.claude/skills', 'skills-lock.json')
    }

    It 'run each job on a pinned runner image, never a moving -latest one' {
        foreach ($workflow in $script:Workflows) {
            $text = Get-Content -Raw -LiteralPath $workflow.FullName
            $code = @(Get-WorkflowCodeLines $text)
            @($code | Where-Object { $_ -match '-latest' }) | Should -BeNullOrEmpty -Because "$($workflow.Name) names a floating image"
            $jobs = @(Get-WorkflowJobs $text)
            $jobs.Count | Should -BeGreaterThan 0 -Because $workflow.Name
            foreach ($job in $jobs) {
                if ($job.CallsWorkflow) { continue }
                @($job.Runners).Count | Should -BeGreaterThan 0 -Because "$($workflow.Name) job $($job.Name) has no pinned runs-on"
                foreach ($runner in $job.Runners) {
                    $runner | Should -Not -Match '-latest' -Because "$($workflow.Name) job $($job.Name)"
                    $runner | Should -Not -Match '^[>|]' -Because "$($workflow.Name) job $($job.Name) left the image on the next line"
                }
            }
        }
    }

    It 'give wingetcreate its token through the environment, never on its command line' {
        $code = @(Get-WorkflowCodeLines (Get-Content -Raw -LiteralPath ($script:Workflows | Where-Object Name -EQ 'package.yml').FullName))
        $values = @(
            foreach ($line in $code) {
                if ($line -match '^\s*WINGET_CREATE_GITHUB_TOKEN:\s*(.+)$') { $Matches[1].Trim() }
            }
        )
        $values | Should -Be '${{ secrets.WINGET_TOKEN }}'
        @($code | Where-Object { $_ -match '--token' }) | Should -BeNullOrEmpty
    }

    It 'commit only the two skills folders and their lock in a skills update, and never merge it' {
        $code = @(Get-WorkflowCodeLines (Get-Content -Raw -LiteralPath ($script:Workflows | Where-Object Name -EQ 'skills.yml').FullName))
        $adds = @(
            foreach ($line in $code) {
                foreach ($tokens in (Get-GitArguments $line 'add')) { ,$tokens }
            }
        )
        $adds.Count | Should -BeGreaterThan 0
        foreach ($tokens in $adds) { Assert-GitTokens $tokens (@('--') + $script:SkillPaths) }
        $diffs = @(
            foreach ($line in $code) {
                foreach ($tokens in (Get-GitArguments $line 'diff')) {
                    if (@($tokens) -contains '--cached') { ,$tokens }
                }
            }
        )
        $diffs.Count | Should -Be 1
        Assert-GitTokens $diffs[0] (@('--cached', '--quiet', 'origin/$BRANCH', '--') + $script:SkillPaths)
        @($code | Where-Object { $_ -match 'gh pr merge|--auto|commit\s+-a' }) | Should -BeNullOrEmpty
    }

    It 'fail the required ci check when .claude/skills is not a copy of .agents/skills' {
        $code = @(Get-WorkflowCodeLines (Get-Content -Raw -LiteralPath ($script:Workflows | Where-Object Name -EQ 'ci.yml').FullName))
        $copies = @(
            foreach ($line in $code) {
                foreach ($tokens in (Get-GitArguments $line 'diff')) {
                    if ((@($tokens) -contains '--no-index') -and (@($tokens) -contains '--exit-code')) { ,$tokens }
                }
            }
        )
        $copies.Count | Should -Be 1
        Assert-GitTokens $copies[0] @('--no-index', '--exit-code', '.agents/skills', '.claude/skills')
    }

    It 'have skills update write every skill into both folders as files, not links' {
        $lock = (Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../skills-lock.json') | ConvertFrom-Json -AsHashtable).skills
        $lock.Keys | Where-Object { $lock[$_].installMode -ne 'copy' } | Should -BeNullOrEmpty
    }

    It 'ties the commit types and the size bounds in the workflows to the lists in CONTRIBUTING.md, while GitHub label descriptions stay a manual step it cannot see' {
        $contributing = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../CONTRIBUTING.md')
        $title = Get-Content -Raw -LiteralPath ($script:Workflows | Where-Object Name -EQ 'pr-title.yml').FullName
        $size = Get-Content -Raw -LiteralPath ($script:Workflows | Where-Object Name -EQ 'pr-size.yml').FullName

        $contributingLines = $contributing -split '\r?\n'
        $typeTableIndex = [array]::IndexOf($contributingLines, '| Type | Use it for |')
        $typeTableIndex | Should -BeGreaterThan -1
        $documentedTypes = @(
            for ($i = $typeTableIndex + 2; $i -lt $contributingLines.Count -and $contributingLines[$i] -match '^\| `([a-z]+)` \|'; $i++) {
                $Matches[1]
            }
        )
        $title -match "(?m)^\s*types='([^']+)'" | Should -BeTrue
        $branchTypes = @($Matches[1] -split '\|')
        $actionTypes = @()
        $readingTypes = $false
        foreach ($line in ($title -split '\r?\n')) {
            if (-not $readingTypes) {
                if ($line -match '^\s*types: \|$') { $readingTypes = $true }
                continue
            }
            if ($line -match '^\s*([a-z]+)\s*$') {
                $actionTypes += $Matches[1]
                continue
            }
            break
        }
        $documentedTypeList = $documentedTypes -join '|'
        $documentedTypeList | Should -Not -BeNullOrEmpty
        ($branchTypes -join '|') | Should -BeExactly $documentedTypeList
        ($actionTypes -join '|') | Should -BeExactly $documentedTypeList

        $contributing -match 'by its changed lines: (?<list>[^.]+)\.' | Should -BeTrue
        $documentedBounds = @()
        foreach ($part in ($Matches['list'] -split ', and |, ')) {
            if ($part -match '^([A-Z]+) under (\d+)$') {
                $documentedBounds += "$($Matches[1])<$($Matches[2])"
            }
            elseif ($part -match '^([A-Z]+) (\d+) or more$') {
                $documentedBounds += "$($Matches[1])>=$($Matches[2])"
            }
            else {
                throw "CONTRIBUTING.md lists '$part', which is not a size bound."
            }
        }
        # The workflow gives XL no number of its own: it is every count the last -lt does not take.
        $workflowBounds = @()
        $lastUnder = $null
        foreach ($line in ($size -split '\r?\n')) {
            if ($line -match '^\s*(?:el)?if \[ "\$lines" -lt (\d+) \]; then size=size:([A-Z]+)\s*$') {
                $lastUnder = $Matches[1]
                $workflowBounds += "$($Matches[2])<$lastUnder"
            }
            elseif ($line -match '^\s*else size=size:([A-Z]+)\s*$') {
                $lastUnder | Should -Not -BeNullOrEmpty
                $workflowBounds += "$($Matches[1])>=$lastUnder"
            }
        }
        $documentedBoundList = $documentedBounds -join ', '
        $documentedBoundList | Should -Not -BeNullOrEmpty
        ($workflowBounds -join ', ') | Should -BeExactly $documentedBoundList
    }
}
