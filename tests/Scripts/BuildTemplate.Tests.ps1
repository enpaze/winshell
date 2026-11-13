BeforeAll {
    $script:Repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
}

Describe 'Compiling template sources against the current libraries' {
    BeforeEach {
        $script:Fixture = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        foreach ($folder in @('scripts', 'src/Winshell.Shell', 'templates/winshell')) {
            New-Item -ItemType Directory -Path (Join-Path $script:Fixture $folder) -Force | Out-Null
        }
        foreach ($path in @('scripts/build-template.ps1', '.editorconfig', 'global.json', 'Directory.Build.props', 'Directory.Build.targets')) {
            Copy-Item -LiteralPath (Join-Path $script:Repository $path) -Destination (Join-Path $script:Fixture $path)
        }
        [IO.File]::WriteAllText((Join-Path $script:Fixture 'src/Winshell.Shell/Winshell.Shell.csproj'), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup></Project>')
        [IO.File]::WriteAllText((Join-Path $script:Fixture 'src/Winshell.Shell/Api.cs'), "namespace Winshell;`n`npublic static class Api`n{`n    public static string Message => `"current`";`n}`n")
        $script:Project = Join-Path $script:Fixture 'templates/winshell/MyShell.csproj'
        [IO.File]::WriteAllText($script:Project, '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net11.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><PackageReference Include="Winshell" Version="__WINSHELL_VERSION__" /></ItemGroup></Project>')
        git -C $script:Fixture init --quiet
        if ($LASTEXITCODE -ne 0) { throw 'Could not initialize fixture repository.' }
        git -C $script:Fixture add .
        if ($LASTEXITCODE -ne 0) { throw 'Could not stage fixture files.' }
    }

    It '<Behavior>' -ForEach @(
        @{ Behavior = 'accepts a template using the current API'; Code = 'namespace MyShell; internal static class Program { public static void Main() { System.Console.WriteLine(Winshell.Api.Message); } }'; Diagnostic = $null }
        @{ Behavior = 'rejects a template using a missing API'; Code = 'namespace MyShell; internal static class Program { public static void Main() { System.Console.WriteLine(Winshell.Api.Missing); } }'; Diagnostic = 'CS0117' }
        @{ Behavior = 'enforces the configured namespace style'; Code = 'namespace MyShell { internal static class Program { public static void Main() { System.Console.WriteLine(Winshell.Api.Message); } } }'; Diagnostic = 'IDE0161' }
        @{ Behavior = 'enforces the configured using placement'; Code = 'namespace MyShell; using System; internal static class Program { public static void Main() { Console.WriteLine(Winshell.Api.Message); } }'; Diagnostic = 'IDE0065' }
    ) {
        [IO.File]::WriteAllText((Join-Path $script:Fixture 'templates/winshell/Program.cs'), $Code)
        $projectHash = (Get-FileHash -LiteralPath $script:Project).Hash
        $index = Join-Path $script:Fixture '.git/index'
        $indexHash = (Get-FileHash -LiteralPath $index).Hash
        $output = pwsh -NoProfile -File (Join-Path $script:Fixture 'scripts/build-template.ps1') 2>&1
        $buildExit = $LASTEXITCODE
        if ($Diagnostic) {
            $buildExit | Should -Not -Be 0 -Because ($output -join "`n")
            ($output -join "`n") | Should -Match $Diagnostic
        }
        else {
            $buildExit | Should -Be 0 -Because ($output -join "`n")
        }
        (Get-FileHash -LiteralPath $script:Project).Hash | Should -Be $projectHash
        (Get-FileHash -LiteralPath $index).Hash | Should -Be $indexHash
    }
}
