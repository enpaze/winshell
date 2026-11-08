BeforeAll {
    $script:Repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path

    function Invoke-FixtureFormat {
        param([switch] $VerifyNoChanges)
        $output = pwsh -NoProfile -File (Join-Path $script:Fixture 'scripts/format.ps1') -VerifyNoChanges:$VerifyNoChanges 2>&1
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
    }
}

Describe 'C# formatting and template compilation' {
    BeforeEach {
        $script:Fixture = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        foreach ($folder in @('scripts', 'src/Winshell.Shell', 'templates/winshell')) {
            New-Item -ItemType Directory -Path (Join-Path $script:Fixture $folder) -Force | Out-Null
        }
        foreach ($path in @('scripts/format.ps1', '.editorconfig', 'global.json')) {
            Copy-Item -LiteralPath (Join-Path $script:Repository $path) -Destination (Join-Path $script:Fixture $path)
        }
        [IO.File]::WriteAllText((Join-Path $script:Fixture 'winshell.slnx'), '<Solution><Project Path="src/Winshell.Shell/Winshell.Shell.csproj" /></Solution>')
        [IO.File]::WriteAllText((Join-Path $script:Fixture 'src/Winshell.Shell/Winshell.Shell.csproj'), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
        [IO.File]::WriteAllText((Join-Path $script:Fixture 'src/Winshell.Shell/Api.cs'), "namespace Winshell;`n`npublic static class Api`n{`n    public static string Message => `"current`";`n}`n")
        $script:Project = Join-Path $script:Fixture 'templates/winshell/MyShell.csproj'
        [IO.File]::WriteAllText($script:Project, '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><PackageReference Include="Winshell" Version="__WINSHELL_VERSION__" /></ItemGroup></Project>')
        $script:Program = Join-Path $script:Fixture 'templates/winshell/Program.cs'
        [IO.File]::WriteAllText($script:Program, 'internal static class Program{public static void Main(){System.Console.WriteLine(Winshell.Api.Message);}}')
        git -C $script:Fixture init --quiet
        if ($LASTEXITCODE -ne 0) { throw 'Could not initialize fixture repository.' }
        git -C $script:Fixture add .
        if ($LASTEXITCODE -ne 0) { throw 'Could not stage fixture files.' }
        $script:Index = Join-Path $script:Fixture '.git/index'
        $script:IndexHash = (Get-FileHash -LiteralPath $script:Index).Hash
        $script:ProjectHash = (Get-FileHash -LiteralPath $script:Project).Hash
    }

    It 'verifies without editing source or index, then applies template formatting without changing its package reference' {
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

    It 'rejects template code that cannot compile against the current libraries' {
        [IO.File]::WriteAllText($script:Program, 'internal static class Program{public static void Main(){System.Console.WriteLine(Winshell.Api.Missing);}}')
        $check = Invoke-FixtureFormat -VerifyNoChanges
        $check.ExitCode | Should -Not -Be 0 -Because $check.Output
        $check.Output | Should -Match 'CS0117'
        (Get-FileHash -LiteralPath $script:Project).Hash | Should -Be $script:ProjectHash
    }
}
