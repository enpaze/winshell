BeforeAll {
    $script:Script = (Resolve-Path (Join-Path $PSScriptRoot '../../scripts/test-framework.ps1')).Path
    $script:Banned = 'x' + 'unit'

    function Invoke-TestFramework([string] $Root) {
        $output = pwsh -NoProfile -File $script:Script -Root $Root 2>&1
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
    }
}

Describe 'One test framework' {
    It 'fails and names a tracked file that references another test framework' {
        $fixture = Join-Path $TestDrive 'banned'
        New-Item -ItemType Directory -Path (Join-Path $fixture 'tests') -Force | Out-Null
        git -C $fixture init --quiet
        Set-Content -LiteralPath (Join-Path $fixture 'tests/Directory.Build.props') -Value "<Project><ItemGroup><PackageReference Include=`"$($script:Banned.ToUpperInvariant()).v3`" /></ItemGroup></Project>"
        Set-Content -LiteralPath (Join-Path $fixture 'kept.cs') -Value 'public static class Kept;'
        git -C $fixture add --all
        $result = Invoke-TestFramework $fixture
        $result.ExitCode | Should -Be 1
        $result.Output | Should -Match '^tests/Directory\.Build\.props names .*MSTest 4'
        $result.Output | Should -Not -Match 'kept\.cs'
    }

    It 'passes on this repository' {
        $result = Invoke-TestFramework (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
        $result.Output | Should -Match '^No tracked file names '
        $result.ExitCode | Should -Be 0
    }
}
