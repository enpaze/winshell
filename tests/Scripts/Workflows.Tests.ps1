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
            Get-WorkflowCode $workflow.FullName | Should -Not -Match '(?i)(windows|ubuntu|macos)-latest' -Because $workflow.Name
        }
    }

    It 'publishes both architectures to the rolling dev tag' {
        $dev = Get-Workflow 'dev-release.yml'
        $dev | Should -Match 'windows-2025'
        $dev | Should -Match 'windows-11-arm'
        $dev | Should -Match 'win-x64'
        $dev | Should -Match 'win-arm64'
        $dev | Should -Match 'SHA256SUMS\.txt'
        $dev | Should -Match 'version\.json'
        $dev | Should -Match '--clobber'
        $dev | Should -Match '--latest=false'
        @($dev | Select-String -Pattern 'contents: write' -AllMatches).Matches.Count | Should -Be 1
    }

    It 'require the feature drivers through ci whenever the code changes' {
        $ci = Get-Workflow 'ci.yml'
        $ci | Should -Match '(?m)^  features:\n    name: features\n    needs: changes\n    if: .*needs\.changes\.outputs\.code != ''false'''
        $ci | Should -Match '\./\.agents/skills/verify-winshell/scripts/drive\.ps1 -Session'
        $ci | Should -Match '(?m)^    needs: \[quality, tests, installer, comments, features\]$'
        $ci | Should -Match ([regex]::Escape('test "$FEATURES_RESULT" = success || test "$FEATURES_RESULT" = skipped'))
    }

    It 'give wingetcreate its token through the environment, never on its command line' {
        $package = Get-Workflow 'package.yml'
        $package | Should -Match ([regex]::Escape('WINGET_CREATE_GITHUB_TOKEN: ${{ secrets.WINGET_TOKEN }}'))
        $package | Should -Not -Match '--token'
    }
}
