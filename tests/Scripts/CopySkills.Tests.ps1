BeforeAll {
    $script:Script = (Resolve-Path (Join-Path $PSScriptRoot '../../scripts/copy-skills.ps1')).Path

    # A checkout of its own with the script in it, so the script finds its folders where it finds them in the repo.
    function New-Checkout {
        $root = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path (Join-Path $root 'scripts') | Out-Null
        Copy-Item -LiteralPath $script:Script -Destination (Join-Path $root 'scripts')
        foreach ($folder in '.agents/skills', '.claude/skills') {
            New-Item -ItemType Directory -Path (Join-Path $root "$folder/tdd/agents") | Out-Null
            Set-Content -LiteralPath (Join-Path $root "$folder/tdd/SKILL.md") -Value '# TDD' -NoNewline
            Set-Content -LiteralPath (Join-Path $root "$folder/tdd/agents/openai.yaml") -Value 'name: tdd' -NoNewline
        }
        $root
    }

    function Invoke-Check {
        param([string] $Root)
        $output = pwsh -NoProfile -File (Join-Path $Root 'scripts/copy-skills.ps1') -Check 2>&1
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $output -join "`n" }
    }
}

Describe 'scripts/copy-skills.ps1' {
    It 'passes the check when the two folders hold the same files' {
        $result = Invoke-Check (New-Checkout)
        $result.ExitCode | Should -Be 0
    }

    It 'fails the check on <Name>, naming the file' -ForEach @(
        @{ Name = 'a file that differs'; Path = '.claude/skills/tdd/SKILL.md'; Change = 'Edit' },
        @{ Name = 'an extra file in .claude/skills'; Path = '.claude/skills/tdd/agents/extra.yaml'; Change = 'Add' },
        @{ Name = 'a file missing from .claude/skills'; Path = '.claude/skills/tdd/agents/openai.yaml'; Change = 'Remove' }
    ) {
        $root = New-Checkout
        $file = Join-Path $root $Path
        switch ($Change) {
            'Edit' { Set-Content -LiteralPath $file -Value '# TDD, changed' -NoNewline }
            'Add' { Set-Content -LiteralPath $file -Value 'new' -NoNewline }
            'Remove' { Remove-Item -LiteralPath $file }
        }
        $result = Invoke-Check $root
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match ([regex]::Escape(($Path -replace '^\.(agents|claude)/skills/')))
    }

    It 'makes .claude/skills a copy of .agents/skills, dropping what .agents/skills lacks' {
        $root = New-Checkout
        Set-Content -LiteralPath (Join-Path $root '.agents/skills/tdd/SKILL.md') -Value '# TDD, newer' -NoNewline
        New-Item -ItemType Directory -Path (Join-Path $root '.agents/skills/grill-me') | Out-Null
        Set-Content -LiteralPath (Join-Path $root '.agents/skills/grill-me/SKILL.md') -Value '# Grill me' -NoNewline
        Set-Content -LiteralPath (Join-Path $root '.claude/skills/tdd/old.md') -Value 'gone upstream' -NoNewline

        pwsh -NoProfile -File (Join-Path $root 'scripts/copy-skills.ps1')
        $LASTEXITCODE | Should -Be 0

        Get-Content -Raw -LiteralPath (Join-Path $root '.claude/skills/tdd/SKILL.md') | Should -Be '# TDD, newer'
        Get-Content -Raw -LiteralPath (Join-Path $root '.claude/skills/grill-me/SKILL.md') | Should -Be '# Grill me'
        Join-Path $root '.claude/skills/tdd/old.md' | Should -Not -Exist
        (Invoke-Check $root).ExitCode | Should -Be 0
    }

    # The skills tool links each skill it updates into .claude/skills.
    It 'fails the check on a skill linked into .claude/skills, and replaces the link with a copy' {
        $root = New-Checkout
        Remove-Item -LiteralPath (Join-Path $root '.claude/skills/tdd') -Recurse
        New-Item -ItemType ($IsWindows ? 'Junction' : 'SymbolicLink') -Path (Join-Path $root '.claude/skills/tdd') -Target (Join-Path $root '.agents/skills/tdd') | Out-Null
        (Invoke-Check $root).ExitCode | Should -Be 1

        pwsh -NoProfile -File (Join-Path $root 'scripts/copy-skills.ps1')
        $LASTEXITCODE | Should -Be 0

        (Get-Item -LiteralPath (Join-Path $root '.claude/skills/tdd') -Force).LinkType | Should -BeNullOrEmpty
        Get-Content -Raw -LiteralPath (Join-Path $root '.agents/skills/tdd/SKILL.md') | Should -Be '# TDD'
        (Invoke-Check $root).ExitCode | Should -Be 0
    }
}
