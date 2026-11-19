BeforeAll {
    $script:Skill = (Resolve-Path (Join-Path $PSScriptRoot '../../.agents/skills/verify-winshell')).Path
    $script:Drive = Join-Path $script:Skill 'scripts/drive.ps1'

    function script:Copy-Map {
        $map = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        Copy-Item -LiteralPath (Join-Path $script:Skill 'features') -Destination $map -Recurse
        $map
    }

    function script:Get-FrontMatter([string] $Path) {
        $text = [IO.File]::ReadAllText($Path) -replace "`r`n", "`n"
        [regex]::Match($text, '\A---\n.*?\n---\n', 'Singleline').Value
    }
}

Describe 'The verify-winshell feature map' {
    It 'has every feature file in the shape its README describes' {
        $output = & $script:Drive -Check
        $LASTEXITCODE | Should -Be 0 -Because ($output -join "`n")
    }

    It 'names the fix when a file loses a section' {
        $map = Copy-Map
        $file = Join-Path $map 'service.audio.md'
        $text = [IO.File]::ReadAllText($file)
        [IO.File]::WriteAllText($file, $text.Substring(0, $text.IndexOf('## Gotchas')))
        $output = & $script:Drive -Check -Map $map
        $LASTEXITCODE | Should -Be 1
        $output -join "`n" | Should -Match 'MAP service\.audio\.md: the H2 sections are exactly .*"Gotchas", in that order'
    }

    It 'names the fix when a step bullet drifts from its step' {
        $map = Copy-Map
        $file = Join-Path $map 'command.list.md'
        [IO.File]::WriteAllText($file, [IO.File]::ReadAllText($file).Replace('Run `winshell list -j`', 'Run `winshell list`'))
        $output = & $script:Drive -Check -Map $map
        $LASTEXITCODE | Should -Be 1
        $output -join "`n" | Should -Match 'MAP command\.list\.md: step bullet 2 names its command exactly: `winshell list -j`'
    }

    It 'names the fix when the index misses a feature' {
        $map = Copy-Map
        $readme = Join-Path $map 'README.md'
        [IO.File]::WriteAllLines($readme, @([IO.File]::ReadAllLines($readme) | Where-Object { $_ -notmatch '\(\./service\.cpu\.md\)' }))
        $output = & $script:Drive -Check -Map $map
        $LASTEXITCODE | Should -Be 1
        $output -join "`n" | Should -Match 'MAP README\.md: Features does not link \./service\.cpu\.md'
    }

    It 'names the fix when a step takes a key the driver does not know' {
        $map = Copy-Map
        $file = Join-Path $map 'command.help.md'
        [IO.File]::WriteAllText($file, [IO.File]::ReadAllText($file).Replace('"output":', '"expect":'))
        $output = & $script:Drive -Check -Map $map
        $LASTEXITCODE | Should -Be 1
        $output -join "`n" | Should -Match 'MAP command\.help\.md: step 1 has "expect", which a winshell step does not take'
    }
}

Describe 'The Claude Code entry point of verify-winshell' {
    It 'repeats the front matter of the skill' {
        $entry = Join-Path $PSScriptRoot '../../.claude/skills/verify-winshell/SKILL.md'
        Get-FrontMatter $entry | Should -BeExactly (Get-FrontMatter (Join-Path $script:Skill 'SKILL.md')) -Because 'Claude Code reads .claude/skills/verify-winshell/SKILL.md; copy the front matter of .agents/skills/verify-winshell/SKILL.md into it'
    }

    It 'sends the reader to the skill' {
        $entry = Join-Path $PSScriptRoot '../../.claude/skills/verify-winshell/SKILL.md'
        Get-Content -LiteralPath $entry -Raw | Should -Match ([regex]::Escape('(../../../.agents/skills/verify-winshell/SKILL.md)'))
    }
}
