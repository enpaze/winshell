BeforeAll {
    $script:Repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path

    function Invoke-FixtureFormat {
        param([switch] $VerifyNoChanges)
        $output = pwsh -NoProfile -File (Join-Path $script:Fixture 'scripts/format.ps1') -VerifyNoChanges:$VerifyNoChanges 2>&1
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
    }
}

Describe 'C# file formatting' {
    BeforeEach {
        $script:Fixture = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        foreach ($folder in @('scripts', 'src/Winshell.Shell', 'templates/winshell')) {
            New-Item -ItemType Directory -Path (Join-Path $script:Fixture $folder) -Force | Out-Null
        }
        foreach ($path in @('scripts/format.ps1', '.editorconfig', 'global.json')) {
            Copy-Item -LiteralPath (Join-Path $script:Repository $path) -Destination (Join-Path $script:Fixture $path)
        }
        [IO.File]::WriteAllText((Join-Path $script:Fixture '.gitignore'), "bin/`nobj/`n")
        [IO.File]::WriteAllText((Join-Path $script:Fixture 'src/Winshell.Shell/Api.cs'), "namespace Winshell;`n`npublic static class Api`n{`n    public static string Message => `"current`";`n}`n")
        $script:Project = Join-Path $script:Fixture 'templates/winshell/MyShell.csproj'
        [IO.File]::WriteAllText($script:Project, '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="Winshell" Version="__WINSHELL_VERSION__" /></ItemGroup></Project>')
        $script:Program = Join-Path $script:Fixture 'templates/winshell/Program.cs'
        [IO.File]::WriteAllText($script:Program, "internal static class Program`n{`n    public static void Main() { }`n}`n")
        git -C $script:Fixture init --quiet
        if ($LASTEXITCODE -ne 0) { throw 'Could not initialize fixture repository.' }
        git -C $script:Fixture add .
        if ($LASTEXITCODE -ne 0) { throw 'Could not stage fixture files.' }
        $script:Index = Join-Path $script:Fixture '.git/index'
        $script:IndexHash = (Get-FileHash -LiteralPath $script:Index).Hash
        $script:ProjectHash = (Get-FileHash -LiteralPath $script:Project).Hash
    }

    It 'verifies without editing source or index, then formats a template without restoring its package reference' {
        [IO.File]::WriteAllText($script:Program, 'internal static class Program{public static void Main(){}}')
        $sourceHash = (Get-FileHash -LiteralPath $script:Program).Hash
        $check = Invoke-FixtureFormat -VerifyNoChanges
        $check.ExitCode | Should -Not -Be 0 -Because $check.Output
        $check.Output | Should -Match 'WHITESPACE'
        (Get-FileHash -LiteralPath $script:Program).Hash | Should -Be $sourceHash
        (Get-FileHash -LiteralPath $script:Project).Hash | Should -Be $script:ProjectHash
        (Get-FileHash -LiteralPath $script:Index).Hash | Should -Be $script:IndexHash

        $format = Invoke-FixtureFormat
        $format.ExitCode | Should -Be 0 -Because $format.Output
        (Get-FileHash -LiteralPath $script:Program).Hash | Should -Not -Be $sourceHash
        (Get-FileHash -LiteralPath $script:Project).Hash | Should -Be $script:ProjectHash
        (Get-FileHash -LiteralPath $script:Index).Hash | Should -Be $script:IndexHash

        $again = Invoke-FixtureFormat -VerifyNoChanges
        $again.ExitCode | Should -Be 0 -Because $again.Output
    }

    It 'includes untracked source paths with spaces, leaves ignored output alone and tolerates deleted sources' {
        $folder = Join-Path $script:Fixture 'docs/samples/new widget'
        $ignored = Join-Path $folder 'obj/Ignored.cs'
        New-Item -ItemType Directory -Path (Split-Path -Parent $ignored) -Force | Out-Null
        $added = Join-Path $folder 'Added.cs'
        [IO.File]::WriteAllText($added, 'internal static class Added{public static int Value=>1;}')
        [IO.File]::WriteAllText($ignored, 'internal static class Ignored{public static int Value=>1;}')
        $ignoredHash = (Get-FileHash -LiteralPath $ignored).Hash
        Remove-Item -LiteralPath (Join-Path $script:Fixture 'src/Winshell.Shell/Api.cs')

        $check = Invoke-FixtureFormat -VerifyNoChanges
        $check.ExitCode | Should -Not -Be 0 -Because $check.Output
        $check.Output | Should -Match 'Added.cs.*WHITESPACE'
        $format = Invoke-FixtureFormat
        $format.ExitCode | Should -Be 0 -Because $format.Output
        $again = Invoke-FixtureFormat -VerifyNoChanges
        $again.ExitCode | Should -Be 0 -Because $again.Output
        (Get-FileHash -LiteralPath $ignored).Hash | Should -Be $ignoredHash
        (Get-FileHash -LiteralPath $script:Index).Hash | Should -Be $script:IndexHash
    }
}
