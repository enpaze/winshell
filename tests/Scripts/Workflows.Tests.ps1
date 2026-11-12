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

# What a workflow runs: its text without comments. A comment can repeat a command without running it.
function script:Get-WorkflowCode([string] $Path) {
    $code = foreach ($line in (Get-Content -LiteralPath $Path)) {
        $kept = Remove-TrailingComment $line
        if ($kept.Trim()) { $kept }
    }
    return $code -join "`n"
}

Describe 'The workflows' {
    BeforeAll {
        $script:Workflows = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '../../.github/workflows') -File
        function script:Get-Workflow([string] $Name) { Get-WorkflowCode ($script:Workflows | Where-Object Name -EQ $Name).FullName }
    }

    It 'run each job on a pinned runner image, never a moving -latest one' {
        foreach ($workflow in $script:Workflows) {
            Get-WorkflowCode $workflow.FullName | Should -Not -Match '-latest' -Because $workflow.Name
        }
    }

    It 'give wingetcreate its token through the environment, never on its command line' {
        $package = Get-Workflow 'package.yml'
        $package | Should -Match ([regex]::Escape('WINGET_CREATE_GITHUB_TOKEN: ${{ secrets.WINGET_TOKEN }}'))
        $package | Should -Not -Match '--token'
    }

    It 'commit only the two skills folders and their lock in a skills update, and never merge it' {
        $skills = Get-Workflow 'skills.yml'
        $skills | Should -Match ([regex]::Escape('git add -- .agents/skills .claude/skills skills-lock.json'))
        $skills | Should -Not -Match '(?m)git add (?!-- \.agents/skills \.claude/skills skills-lock\.json$)'
        $skills | Should -Match ([regex]::Escape('git diff --cached --quiet "origin/$BRANCH" -- .agents/skills .claude/skills skills-lock.json'))
        $skills | Should -Not -Match 'gh pr merge|--auto|commit\s+-a'
    }

    It 'fail the required ci check when .claude/skills is not a copy of .agents/skills' {
        Get-Workflow 'ci.yml' | Should -Match ([regex]::Escape('git diff --no-index --exit-code .agents/skills .claude/skills'))
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
