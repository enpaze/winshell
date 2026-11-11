Describe 'The workflows' {
    BeforeAll {
        $script:Workflows = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '../../.github/workflows') -File
    }

    It 'run each job on a pinned runner image, never a moving -latest one' {
        foreach ($workflow in $script:Workflows) {
            Get-Content -Raw -LiteralPath $workflow.FullName | Should -Not -Match '-latest' -Because $workflow.Name
        }
    }

    It 'give wingetcreate its token through the environment, never on its command line' {
        $package = Get-Content -Raw -LiteralPath ($script:Workflows | Where-Object Name -EQ 'package.yml').FullName
        $package | Should -Match ([regex]::Escape('WINGET_CREATE_GITHUB_TOKEN: ${{ secrets.WINGET_TOKEN }}'))
        $package | Should -Not -Match '--token'
    }

    It 'commit only the two skills folders and their lock in a skills update, and never merge it' {
        $skills = Get-Content -Raw -LiteralPath ($script:Workflows | Where-Object Name -EQ 'skills.yml').FullName
        $skills | Should -Match ([regex]::Escape('git add -- .agents/skills .claude/skills skills-lock.json'))
        $skills | Should -Not -Match '(?m)git add (?!-- \.agents/skills \.claude/skills skills-lock\.json$)'
        $skills | Should -Match ([regex]::Escape('git diff --cached --quiet "origin/$BRANCH" -- .agents/skills .claude/skills skills-lock.json'))
        $skills | Should -Not -Match 'gh pr merge|--auto|commit -a'
    }

    It 'fail the required ci check when .claude/skills is not a copy of .agents/skills' {
        $ci = Get-Content -Raw -LiteralPath ($script:Workflows | Where-Object Name -EQ 'ci.yml').FullName
        $ci | Should -Match ([regex]::Escape('git diff --no-index --exit-code .agents/skills .claude/skills'))
    }

    It 'have skills update write every skill into both folders as files, not links' {
        $lock = (Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../skills-lock.json') | ConvertFrom-Json -AsHashtable).skills
        $lock.Keys | Where-Object { $lock[$_].installMode -ne 'copy' } | Should -BeNullOrEmpty
    }

    It 'ties the commit types and the size bounds in the workflows to the lists in CONTRIBUTING.md, while GitHub label descriptions stay a manual step it cannot see' {
        $contributing = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../CONTRIBUTING.md')
        $title = Get-Content -Raw -LiteralPath ($script:Workflows | Where-Object Name -EQ 'pr-title.yml').FullName
        $size = Get-Content -Raw -LiteralPath ($script:Workflows | Where-Object Name -EQ 'pr-size.yml').FullName

        $table = $contributing -split '\r?\n'
        $header = [array]::IndexOf($table, '| Type | Use it for |')
        $header | Should -BeGreaterThan -1
        $documentedTypes = @(
            for ($i = $header + 2; $i -lt $table.Count -and $table[$i] -match '^\| `([a-z]+)` \|'; $i++) {
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
        $documented = $documentedTypes -join '|'
        $documented | Should -Not -BeNullOrEmpty
        ($branchTypes -join '|') | Should -BeExactly $documented
        ($actionTypes -join '|') | Should -BeExactly $documented

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
        $listed = $documentedBounds -join ', '
        $listed | Should -Not -BeNullOrEmpty
        ($workflowBounds -join ', ') | Should -BeExactly $listed
    }
}
