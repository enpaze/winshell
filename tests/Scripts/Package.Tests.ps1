Describe 'The binaries Winshell signs' {
    It 'are the same for each runtime in SignPath''s artifact configuration, and include every assembly built from src' {
        [xml] $config = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../packaging/signpath/artifact-configuration.xml')
        $runtimes = $config.'artifact-configuration'.'zip-file'.'pe-file-set'.include.path | Group-Object { ($_ -split '/')[0] }
        # The package project only packs the others; it builds nothing that ships.
        $assemblies = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '../../src') -Recurse -Filter '*.csproj' | ForEach-Object {
            $text = Get-Content -Raw -LiteralPath $_.FullName
            if ($text -notmatch '<IsPackable>true</IsPackable>') {
                ($text -match '<AssemblyName>([^<]+)</AssemblyName>' ? $Matches[1] : $_.BaseName) + '.dll'
            }
        }

        $runtimes.Name | Sort-Object | Should -Be 'win-arm64', 'win-x64'
        $binaries = $runtimes[0].Group | ForEach-Object { ($_ -split '/')[1] } | Sort-Object
        $runtimes[1].Group | ForEach-Object { ($_ -split '/')[1] } | Sort-Object | Should -Be $binaries
        $assemblies | Should -Not -BeNullOrEmpty
        foreach ($assembly in $assemblies) { $binaries | Should -Contain $assembly }
    }
}

Describe 'The oldest Windows a release runs on' {
    It 'is Windows 11 22H2 wherever the build, a project, a manifest or doctor states it' {
        $root = Join-Path $PSScriptRoot '../..'
        $windows = '<(?:SupportedOSPlatformVersion>|TargetFramework>net[\d.]+-windows)(10\.0\.\d+\.\d+)<'
        $projects = Get-ChildItem -LiteralPath (Join-Path $root 'src'), (Join-Path $root 'tests'), (Join-Path $root 'templates') -Recurse -File -Include '*.csproj', '*.props' |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
        # Doctor.OldestReleaseBuild is the floor the Windows check compares to. The build numbers in its
        # other conditions, such as Windows 11 against Windows 10, are not that floor.
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
