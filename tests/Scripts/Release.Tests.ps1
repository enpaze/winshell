BeforeAll {
    . (Join-Path $PSScriptRoot '../../scripts/lib/Release.ps1')
}

Describe 'The files a release uploads' {
    It 'include every file the README and the installer scripts download from a release' {
        $text = foreach ($file in 'README.md', 'scripts/install.ps1', 'scripts/uninstall.ps1') {
            Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot "../../$file")
        }
        $downloaded = [regex]::Matches(($text -join "`n"), '(?:latest/download|\$release)/([\w.-]+\.(?:ps1|txt))') |
            ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique

        $downloaded | Should -Not -BeNullOrEmpty
        foreach ($name in $downloaded) { Get-ReleaseAssetName '1.2.3' | Should -Contain $name }
    }
}

Describe 'Finding a Windows SDK tool' {
    BeforeEach {
        $script:ProgramFiles = ${env:ProgramFiles(x86)}
        ${env:ProgramFiles(x86)} = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $script:Kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
    }

    AfterEach { ${env:ProgramFiles(x86)} = $script:ProgramFiles }

    It 'takes it from the newest SDK that has it, by version and not by name' {
        foreach ($sdk in '10.0.9.0', '10.0.22621.0', '10.0.26100.0') {
            New-Item -ItemType Directory -Force -Path (Join-Path $script:Kits "$sdk/x64") | Out-Null
        }
        foreach ($sdk in '10.0.9.0', '10.0.22621.0') { Set-Content -LiteralPath (Join-Path $script:Kits "$sdk/x64/tool.exe") -Value '' }

        Get-WindowsSdkTool tool.exe | Should -Be (Join-Path $script:Kits '10.0.22621.0/x64/tool.exe')
    }

    It 'says to install the SDK when none has it' {
        { Get-WindowsSdkTool tool.exe } | Should -Throw '*Install the Windows SDK*'
    }
}

Describe 'Checking the version winshell.exe reports' {
    It 'accepts <Reported> for 1.2.3' -ForEach @(
        @{ Reported = 'winshell 1.2.3' }
        @{ Reported = 'winshell 1.2.3+abc123' }
    ) {
        $exe = Join-Path $TestDrive 'winshell.cmd'
        Set-Content -LiteralPath $exe -Value "@echo $Reported"
        Assert-WinshellVersion $exe '1.2.3' | Should -Be $Reported
    }

    It 'stops at <Reported> exiting <Exit> for 1.2.3' -ForEach @(
        @{ Reported = 'winshell 1.2.30'; Exit = 0 }
        @{ Reported = 'winshell 1.2.3'; Exit = 1 }
    ) {
        $exe = Join-Path $TestDrive 'winshell.cmd'
        Set-Content -LiteralPath $exe -Value "@echo $Reported`r`n@exit /b $Exit"
        { Assert-WinshellVersion $exe '1.2.3' } | Should -Throw '*not version 1.2.3*'
    }
}

Describe 'Checking the Winshell package a release carries' {
    BeforeEach {
        $script:Release = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:Release | Out-Null
    }

    It 'accepts packages/Winshell.1.2.3.nupkg alone for 1.2.3' {
        New-Item -ItemType File -Force -Path (Join-Path $script:Release 'packages/Winshell.1.2.3.nupkg') | Out-Null
        { Assert-WinshellPackage $script:Release '1.2.3' 'the zip' } | Should -Not -Throw
    }

    It 'stops at <Case> for 1.2.3' -ForEach @(
        @{ Case = 'no packages folder'; Packages = @() }
        @{ Case = 'another version'; Packages = @('Winshell.1.2.2.nupkg') }
        @{ Case = 'another version beside it'; Packages = @('Winshell.1.2.2.nupkg', 'Winshell.1.2.3.nupkg') }
    ) {
        foreach ($name in $Packages) { New-Item -ItemType File -Force -Path (Join-Path $script:Release "packages/$name") | Out-Null }
        { Assert-WinshellPackage $script:Release '1.2.3' 'the zip' } | Should -Throw 'the zip has *, not packages/Winshell.1.2.3.nupkg'
    }
}

Describe 'Packing a zip' {
    It 'orders entries by ordinal path under a culture that sorts differently, and dates them the day it is given' {
        $dir = Join-Path $TestDrive 'files'
        New-Item -ItemType Directory -Path (Join-Path $dir 'sub') | Out-Null
        foreach ($name in 'y', 'i', [string][char]0x012F, 'sub/a', 'Z') {
            Set-Content -LiteralPath (Join-Path $dir $name) -Value $name
        }
        $zip = Join-Path $TestDrive 'out.zip'
        $culture = [Threading.Thread]::CurrentThread.CurrentCulture
        [Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo('lt-LT')
        try { New-Zip $dir $zip ([datetime]::new(2026, 11, 2)) } finally { [Threading.Thread]::CurrentThread.CurrentCulture = $culture }

        $archive = [IO.Compression.ZipFile]::OpenRead($zip)
        try {
            $archive.Entries.FullName -join ',' | Should -BeExactly "Z,i,sub/a,y,$([char]0x012F)"
            $archive.Entries.LastWriteTime.Date | Select-Object -Unique | Should -Be ([datetime]::new(2026, 11, 2))
        } finally { $archive.Dispose() }
    }
}

Describe 'The release version a build stamps' {
    # The version Directory.Build.props stamps, from a copy of that file beside $Manifest. -Missing leaves the file absent.
    BeforeAll {
        function Get-StampedVersion {
            param([string] $Manifest, [switch] $Missing)
            $root = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path (Join-Path $root 'proj') | Out-Null
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../../Directory.Build.props') -Destination (Join-Path $root 'Directory.Build.props')
            if (-not $Missing) {
                Set-Content -LiteralPath (Join-Path $root '.release-please-manifest.json') -Value $Manifest -NoNewline
            }
            $proj = Join-Path $root 'proj/version.proj'
            @'
<Project>
  <Import Project="..\Directory.Build.props" />
  <Target Name="Print">
    <Message Importance="high" Text="VERSION=[$(Version)]" />
  </Target>
</Project>
'@ | Set-Content -LiteralPath $proj
            $out = & dotnet msbuild $proj -t:Print -nologo -v:m 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) { throw "msbuild failed: $out" }
            if ($out -notmatch 'VERSION=\[([^\]]*)\]') { throw "msbuild printed no version: $out" }
            $Matches[1]
        }
    }

    It 'is the manifest''s "." property, not the first number in the file' {
        Get-StampedVersion '{ "other": "9.9.9", ".": "1.2.3-beta.1" }' | Should -Be '1.2.3-beta.1'
    }

    It 'is 0.0.0-dev when the manifest cannot be read' {
        Get-StampedVersion -Missing | Should -Be '0.0.0-dev'
        Get-StampedVersion '{ "other": "9.9.9" }' | Should -Be '0.0.0-dev'
    }

    It 'matches the release the scripts read from this checkout' {
        $manifest = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../.release-please-manifest.json')
        Get-StampedVersion $manifest | Should -Be (Get-ReleaseVersion)
    }
}

Describe 'The release the scripts read' {
    It 'is the manifest''s "." property, not the first number in the file' {
        $root = Join-Path $TestDrive 'repo'
        $lib = Join-Path $root 'scripts/lib/Release.ps1'
        New-Item -ItemType Directory -Path (Split-Path -Parent $lib) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../../scripts/lib/Release.ps1') -Destination $lib
        Set-Content -LiteralPath (Join-Path $root '.release-please-manifest.json') -Value '{ "other": "9.9.9", ".": "1.2.3-beta.1" }' -NoNewline
        $env:WINSHELL_RELEASE_LIB = $lib
        try {
            $version = (& pwsh -NoProfile -Command '. $env:WINSHELL_RELEASE_LIB; Get-ReleaseVersion' | Out-String).Trim()
        } finally {
            Remove-Item Env:\WINSHELL_RELEASE_LIB
        }
        $version | Should -Be '1.2.3-beta.1'
    }
}

Describe 'The Store test identity' {
    It 'is Winshell.Test published by CN=Winshell Test until Partner Center sets one' {
        $identity = Get-StoreTestIdentity
        $identity.Name | Should -Be 'Winshell.Test'
        $identity.Publisher | Should -Be 'CN=Winshell Test'
    }
}

Describe 'The product name' {
    It 'is Winshell, the name the build stamps and the Store manifest is filled with' {
        Get-ProductName | Should -Be 'Winshell'
        $template = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../packaging/msix/AppxManifest.xml')
        $filled = Expand-StoreManifest $template 'Winshell.Test' 'CN=Winshell Test' 'Dante Sparrås' '1.2.3.0' 'x64' (Get-ProductName)
        @([regex]::Matches($filled, '<DisplayName>([^<]*)</DisplayName>') | ForEach-Object { $_.Groups[1].Value }) |
            Should -Be (Get-ProductName)
        $named = @([regex]::Matches($filled, 'DisplayName="([^"]*)"') | ForEach-Object { $_.Groups[1].Value })
        $named | Should -HaveCount 2
        $named | Select-Object -Unique | Should -Be (Get-ProductName)
        $filled | Should -Match 'Application Id="Winshell"'
        $filled | Should -Match 'TaskId="Winshell"'
    }

    # install.ps1 is fetched on its own and can't read Get-ProductName, so its shortcut keeps a literal this test ties.
    It 'is the installer''s shortcut description, and the product SignPath is given' {
        $install = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../scripts/install.ps1')
        ([regex]::Match($install, '\$link\.Description = ''([^'']*)''').Groups[1].Value) | Should -Be (Get-ProductName)
        [xml] $signing = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../packaging/signpath/artifact-configuration.xml')
        $signing.'artifact-configuration'.'zip-file'.'pe-file-set'.'product-name' | Should -Be '${product}'
        $workflow = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../.github/workflows/package.yml')
        $workflow | Should -Match 'product=\$\(Get-ProductName\)'
        $workflow | Should -Match 'steps\.product\.outputs\.product'
    }
}

Describe 'The files a release removes' {
    It 'drops the symbols and helpers the product project lists, and keeps what a release ships' {
        $dir = Join-Path $TestDrive 'publish'
        $removed = @(
            'winshell.pdb',
            'createdump.exe',
            'mscordbi.dll',
            'mscordaccore_amd64.dll',
            'Microsoft.DiaSymReader.Native.amd64.dll',
            'Microsoft.Web.WebView2.Core.dll',
            'native/WebView2Loader.dll',
            'clrgc.dll',
            'clrgcexp.dll',
            'msquic.dll'
        )
        $kept = @(
            'winshell.exe',
            'winshell.dll',
            'Microsoft.UI.Xaml.Phone.dll',
            'en/winshell.mui',
            'clrgc.txt'
        )
        foreach ($name in $removed + $kept) {
            $path = Join-Path $dir $name
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $path) | Out-Null
            Set-Content -LiteralPath $path -Value 'x'
        }

        Remove-ReleaseFile $dir

        $left = @(Get-ChildItem -LiteralPath $dir -Recurse -File | ForEach-Object {
                [IO.Path]::GetRelativePath($dir, $_.FullName).Replace('\', '/')
            } | Sort-Object)
        $left | Should -Be ($kept | Sort-Object)
        Test-Path -LiteralPath (Join-Path $dir 'native') | Should -BeFalse
    }
}

Describe 'A doctor check the release could not run' {
    It 'is the sentence Doctor.Guard returns, not a message that only starts the same way' {
        Test-DoctorCouldNotCheck 'Could not check: InvalidOperationException: no screens' | Should -BeTrue
        Test-DoctorCouldNotCheck 'Could not check the clock' | Should -BeFalse
        Test-DoctorCouldNotCheck 'Windows 11, build 22621.1' | Should -BeFalse
    }
}

Describe 'Running a published winshell' {
    BeforeAll {
        function script:Set-WinshellStub([string] $DoctorJson, [int] $DoctorExit = 0) {
            @"
@echo off
if "%1"=="--version" (
  echo winshell 1.2.3
  exit /b 0
)
echo $DoctorJson
exit /b $DoctorExit
"@ | Set-Content -LiteralPath $script:Exe
        }
    }

    BeforeEach {
        $script:Exe = Join-Path $TestDrive 'winshell.cmd'
    }

    It 'accepts the version and does not run doctor when no folder is given' {
        Set-Content -LiteralPath $script:Exe -Value "@echo winshell 1.2.3`r`n@exit /b 0"
        Assert-WinshellRuns $script:Exe '1.2.3' | Should -Be 'winshell 1.2.3'
    }

    It 'stops when doctor returns the sentence Guard returns' {
        Set-WinshellStub '{"checks":[{"id":"dpi","message":"Could not check: InvalidOperationException: no screens"}]}'
        { Assert-WinshellRuns $script:Exe '1.2.3' (Join-Path $TestDrive 'doctor') } |
            Should -Throw '*could not run its dpi check*'
    }

    It 'accepts a check that failed on this PC' {
        Set-WinshellStub '{"checks":[{"id":"dpi","message":"No screens"}]}' 1
        Assert-WinshellRuns $script:Exe '1.2.3' (Join-Path $TestDrive 'doctor') | Should -Be 'winshell 1.2.3'
    }

    It 'stops when doctor <Case>' -ForEach @(
        @{ Case = 'exits 2'; Json = '{"checks":[{"id":"dpi","message":"ok"}]}'; Exit = 2; Expected = '*exited with 2*' }
        @{ Case = 'prints no checks'; Json = '{"checks":[]}'; Exit = 0; Expected = '*printed no checks*' }
    ) {
        Set-WinshellStub $Json $Exit
        { Assert-WinshellRuns $script:Exe '1.2.3' (Join-Path $TestDrive 'doctor') } | Should -Throw $Expected
    }

    It 'is how the x64 doctor run and the arm64 exe run both check the build' {
        $package = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../scripts/package.ps1')
        $workflow = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../../.github/workflows/package.yml')
        $package | Should -Match 'Assert-WinshellRuns \(Join-Path \$dir ''winshell\.exe''\) \$Version \(Join-Path \$verifyRoot ''doctor''\)'
        $package | Should -Not -Match 'Could not check'
        $arm = ($workflow -split '(?m)^  arm64:\r?\n', 2)[1]
        $arm | Should -Match 'Assert-WinshellRuns \(Join-Path \$dest ''winshell\.exe''\) \$version\s*$'
        $arm | Should -Not -Match 'doctor'
        $arm | Should -Not -Match 'Assert-WinshellVersion'
    }
}

Describe 'Reading a PE header' {
    It 'finds the machine and subsystem .NET reads from this PowerShell' {
        $path = (Get-Process -Id $PID).Path
        $bytes = [IO.File]::ReadAllBytes($path)
        $stream = [IO.File]::OpenRead($path)
        try { $headers = [Reflection.PortableExecutable.PEHeaders]::new($stream) } finally { $stream.Dispose() }
        $layout = Get-PeLayout $bytes

        [BitConverter]::ToUInt16($bytes, $layout.Machine) | Should -Be ([int] $headers.CoffHeader.Machine)
        [BitConverter]::ToUInt16($bytes, $layout.Subsystem) | Should -Be ([int] $headers.PEHeader.Subsystem)
    }
}
