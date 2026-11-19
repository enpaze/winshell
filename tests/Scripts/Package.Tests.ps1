Describe 'The binaries Winshell signs' {
    BeforeAll {
        function script:Get-RuntimeAssembly([string] $Project) {
            $assemblies = @{}
            $pending = [Collections.Generic.Queue[string]]::new()
            $pending.Enqueue([IO.Path]::GetFullPath($Project))
            while ($pending.Count -gt 0) {
                $path = $pending.Dequeue()
                if ($assemblies.ContainsKey($path)) { continue }
                [xml] $xml = Get-Content -Raw -LiteralPath $path
                $name = $xml.SelectSingleNode('/Project/PropertyGroup/AssemblyName')
                $assemblies[$path] = ($name ? $name.InnerText : [IO.Path]::GetFileNameWithoutExtension($path)) + '.dll'
                foreach ($reference in $xml.SelectNodes('/Project/ItemGroup/ProjectReference')) {
                    if ($reference.GetAttribute('ReferenceOutputAssembly') -ne 'false') {
                        $pending.Enqueue([IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $path) $reference.GetAttribute('Include'))))
                    }
                }
            }
            $assemblies.Values | Sort-Object
        }

        $script:RuntimeProject = Join-Path $PSScriptRoot '../../src/Winshell/Winshell.csproj'
    }

    It 'are the same for each runtime in SignPath''s artifact configuration, and include every assembly the runtime zip carries from src' {
        [xml] $config = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../packaging/signpath/artifact-configuration.xml')
        $runtimes = $config.'artifact-configuration'.'zip-file'.'pe-file-set'.include.path | Group-Object { ($_ -split '/')[0] }
        $assemblies = Get-RuntimeAssembly $script:RuntimeProject

        $runtimes.Name | Sort-Object | Should -Be 'win-arm64', 'win-x64'
        $binaries = $runtimes[0].Group | ForEach-Object { ($_ -split '/')[1] } | Sort-Object
        $runtimes[1].Group | ForEach-Object { ($_ -split '/')[1] } | Sort-Object | Should -Be $binaries
        $assemblies | Should -Be ('winshell.dll', 'Winshell.Native.dll', 'Winshell.Core.dll', 'Winshell.Services.dll', 'Winshell.Shell.dll' | Sort-Object)
        foreach ($assembly in $assemblies) { $binaries | Should -Contain $assembly }
    }

    It 'leave out the preference generator, which the runtime zip does not carry' {
        Test-Path -LiteralPath (Join-Path $PSScriptRoot '../../src/Winshell.Generators/Winshell.Generators.csproj') | Should -BeTrue

        Get-RuntimeAssembly $script:RuntimeProject | Should -Not -Contain 'Winshell.Generators.dll'
    }

    It 'count a Roslyn component referenced for its assembly, and skip a project referenced only as an analyzer' {
        $files = @{
            'App/App.csproj' = '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../Component/Component.csproj" /><ProjectReference Include="../Analyzer/Analyzer.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" /></ItemGroup></Project>'
            'Component/Component.csproj' = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><IsRoslynComponent>true</IsRoslynComponent></PropertyGroup></Project>'
            'Analyzer/Analyzer.csproj' = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><IsRoslynComponent>true</IsRoslynComponent></PropertyGroup></Project>'
        }
        foreach ($file in $files.GetEnumerator()) {
            $path = Join-Path $TestDrive $file.Key
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $path) | Out-Null
            Set-Content -LiteralPath $path -Value $file.Value
        }

        Get-RuntimeAssembly (Join-Path $TestDrive 'App/App.csproj') | Should -Be 'App.dll', 'Component.dll'
    }
}

Describe 'The oldest Windows a release runs on' {
    It 'is Windows 11 22H2 wherever the build, a project, a manifest or doctor states it' {
        $root = Join-Path $PSScriptRoot '../..'
        $windows = '<(?:SupportedOSPlatformVersion>|TargetFramework>net[\d.]+-windows)(10\.0\.\d+\.\d+)<'
        $projects = Get-ChildItem -LiteralPath (Join-Path $root 'src'), (Join-Path $root 'tests'), (Join-Path $root 'templates') -Recurse -File -Include '*.csproj', '*.props' |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
        $doctorSource = Get-Content -Raw -LiteralPath (Join-Path $root 'src/Winshell.Services/Diagnostics/Doctor.cs')
        $oldest = [regex]::Matches($doctorSource, 'OldestReleaseBuild = (\d+)')
        $oldest.Count | Should -Be 1
        $declared = @(
            (Select-String -LiteralPath (Join-Path $root 'Directory.Build.props') -Pattern $windows).Matches[0].Groups[1].Value
            $projects | Select-String -Pattern $windows | ForEach-Object { $_.Matches[0].Groups[1].Value }
            (Select-String -LiteralPath (Join-Path $root 'scripts/package.ps1') -Pattern '^MinimumOSVersion: (\S+)').Matches[0].Groups[1].Value
            ([xml] (Get-Content -Raw -LiteralPath (Join-Path $root 'packaging/msix/AppxManifest.xml'))).Package.Dependencies.TargetDeviceFamily.MinVersion
            "10.0.$($oldest[0].Groups[1].Value).0"
        )

        $declared | Select-Object -Unique | Should -Be '10.0.22621.0'
    }
}
