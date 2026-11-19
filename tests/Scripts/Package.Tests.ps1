Describe 'The binaries Winshell signs' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '../../scripts/lib/Release.ps1')
        [xml] $config = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../packaging/signpath/artifact-configuration.xml')
        $script:Runtimes = $config.'artifact-configuration'.'zip-file'.'pe-file-set'.include.path | Group-Object { ($_ -split '/')[0] }

        function script:New-Payload([string[]] $Files) {
            $dir = Join-Path $TestDrive ([guid]::NewGuid())
            foreach ($file in $Files) { New-Item -ItemType File -Force -Path (Join-Path $dir $file) | Out-Null }
            $dir
        }

        function script:Get-Signed([string] $Runtime) {
            ($script:Runtimes | Where-Object Name -EQ $Runtime).Group | ForEach-Object { $_.Substring($Runtime.Length + 1) }
        }
    }

    It 'are the same for each runtime in SignPath''s artifact configuration' {
        $script:Runtimes.Name | Sort-Object | Should -Be 'win-arm64', 'win-x64'
        Get-Signed 'win-arm64' | Sort-Object | Should -Be (Get-Signed 'win-x64' | Sort-Object)
    }

    It 'cover every Winshell assembly a <_> payload carries' -ForEach 'win-x64', 'win-arm64' {
        $signed = Get-Signed $_
        $payload = New-Payload ($signed + 'coreclr.dll', 'Microsoft.UI.Xaml.dll', 'Winshell.Shell.pdb', 'Winshell.Shell.pri')

        { Assert-WinshellSigned $payload $signed 'the zip' } | Should -Not -Throw
    }

    It 'stop a payload that carries <File> unsigned' -ForEach @(
        @{ File = 'Winshell.Extra.dll' }
        @{ File = 'en-US/Winshell.Shell.resources.dll' }
    ) {
        $signed = Get-Signed 'win-x64'
        $payload = New-Payload ($signed + $File)

        { Assert-WinshellSigned $payload $signed 'the zip' } | Should -Throw "the zip carries $File unsigned.*"
    }

    It 'stop a payload that carries the preference generator as <File>, even when it is on the signing list' -ForEach @(
        @{ File = 'Winshell.Generators.dll' }
        @{ File = 'analyzers/Winshell.Generators.dll' }
    ) {
        $signed = @(Get-Signed 'win-x64') + $File
        $payload = New-Payload $signed

        { Assert-WinshellSigned $payload $signed 'the zip' } | Should -Throw "the zip carries $File, an analyzer only the package ships.*"
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
