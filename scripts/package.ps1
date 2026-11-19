#requires -Version 7.0
param(
    [switch] $Publish,
    [switch] $Pack,
    [switch] $Verify,
    [switch] $Signed,
    [string] $Out = 'artifacts',
    [ValidateSet('win-x64', 'win-arm64')]
    [string[]] $Runtime = @('win-x64', 'win-arm64'),
    [string] $Version,
    [string] $Commit,
    [string] $Tag,
    [string] $Repository = 'enpaze/winshell',
    [string] $IdentityName,
    [string] $Publisher,
    [string] $PublisherDisplayName
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'lib/Release.ps1')
if (-not $Publish -and -not $Pack -and -not $Verify) { $Publish = $true; $Pack = $true; $Verify = $true }
if (-not $Version) { $Version = Get-ReleaseVersion }
$dev = $Version -match '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)-dev\.(0|[1-9][0-9]*)$'
if (-not $dev -and $Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must be major.minor.patch, not '$Version'" }
if ($dev) {
    if ($Commit -notmatch '^[0-9a-fA-F]{40}$') { throw "The release commit must be 40 hex digits, not '$Commit'." }
    $head = (git -C $root rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'git could not read the commit of HEAD' }
    $Commit = $Commit.ToLowerInvariant()
    if ($head -cne $Commit) { throw "Checked out $head, expected $Commit." }
}
if ($Tag -and $Tag -cne "v$Version") { throw "The checkout is version $Version, not the release $Tag." }
$product = Get-ProductName
$testIdentity = Get-StoreTestIdentity
if (-not $IdentityName) { $IdentityName = $testIdentity.Name }
if (-not $Publisher) { $Publisher = $testIdentity.Publisher }
$publisherName = 'Dante Sparrås'
if (-not $PublisherDisplayName) { $PublisherDisplayName = $publisherName }
$Out = [IO.Path]::GetFullPath($Out, $root)
$publishRoot = Join-Path $Out 'publish'
$id = 'DanteSparras.Winshell'
$schema = '1.12.0'
$pins = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'tool-pins.psd1')
$wingetDir = Join-Path $Out "winget/$Version"
$day = (git -C $root log -1 --format=%cs)
if ($LASTEXITCODE -ne 0) { throw 'git could not read the date of the commit' }
$releaseDay = [datetime]::ParseExact($day.Trim(), 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

function Assert-Inside([string] $path, [string] $parent) {
    $full = [IO.Path]::GetFullPath($path)
    if (-not $full.StartsWith([IO.Path]::GetFullPath($parent).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "$full is not inside $parent"
    }
    $full
}

function Get-PackageVersion {
    $parts = $Version.Split('.')
    "$([int] $parts[0] + 1).$($parts[1]).$($parts[2]).0"
}

function Get-Sha256([string] $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }

if ($Publish) {
    $packed = Assert-Inside (Join-Path $Out 'pack') $Out
    if (Test-Path -LiteralPath $packed) { Remove-Item -LiteralPath $packed -Recurse -Force }
    & (Join-Path $PSScriptRoot 'pack.ps1') -Out $packed -Version $Version
    $package = Join-Path $packed "Winshell.$Version.nupkg"
    $archive = [IO.Compression.ZipFile]::Open($package, [IO.Compression.ZipArchiveMode]::Update)
    try {
        foreach ($entry in $archive.Entries) { $entry.LastWriteTime = [DateTimeOffset]::new($releaseDay, [TimeSpan]::Zero) }
    } finally { $archive.Dispose() }
    foreach ($rid in $Runtime) {
        $dir = Assert-Inside (Join-Path $publishRoot $rid) $publishRoot
        if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
        $stamp = @('-p:PublishReadyToRun=true', "-p:Version=$Version")
        if ($dev) { $stamp += @("-p:SourceRevisionId=$Commit", '-p:ContinuousIntegrationBuild=true') }
        dotnet publish (Join-Path $root 'src/Winshell/Winshell.csproj') --nologo -c Release -r $rid --self-contained @stamp -o $dir
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish for $rid failed with $LASTEXITCODE" }
        Remove-ReleaseFile $dir
        $gui = [IO.File]::ReadAllBytes((Join-Path $dir 'winshell.exe'))
        $subsystem = (Get-PeLayout $gui).Subsystem
        if ([BitConverter]::ToUInt16($gui, $subsystem) -ne 3) { throw 'winshell.exe is not a console app' }
        $gui[$subsystem] = 2
        [IO.File]::WriteAllBytes((Join-Path $dir 'winshellw.exe'), $gui)
        $pri = Join-Path $dir 'winshell.pri'
        if (Test-Path -LiteralPath $pri) { Copy-Item -LiteralPath $pri -Destination (Join-Path $dir 'winshellw.pri') -Force }
        New-Item -ItemType Directory -Path (Join-Path $dir 'packages') | Out-Null
        Copy-Item -LiteralPath $package -Destination (Join-Path $dir 'packages')
    }
}

if ($Pack) {
    if ($dev) {
        $commit = $Commit
        $recordedVersion = "$Version+$($Commit.Substring(0, 7))"
    } else {
        $commit = (git -C $root rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'git could not read the commit of HEAD' }
        $recordedVersion = $Version
    }
    $versionFile = Join-Path $Out 'version.json'
    Write-ReleaseVersionFile $versionFile $recordedVersion $commit ([datetimeoffset]::UtcNow)
    $sums = [ordered]@{}
    foreach ($rid in $Runtime) {
        $dir = Join-Path $publishRoot $rid
        if (-not (Test-Path -LiteralPath (Join-Path $dir 'winshell.exe'))) { throw "No winshell.exe in $dir. Run with -Publish first." }
        Copy-Item -LiteralPath $versionFile -Destination (Join-Path $dir 'version.json') -Force
        $zip = Join-Path $Out (Get-ReleaseZipName $Version $rid)
        New-Zip $dir $zip $releaseDay
        $sums[(Split-Path -Leaf $zip)] = Get-Sha256 $zip
    }
    $sums['version.json'] = Get-Sha256 $versionFile
    ($sums.GetEnumerator() | ForEach-Object { "$($_.Value)  $($_.Key)" }) -join "`n" |
        Set-Content -LiteralPath (Join-Path $Out 'SHA256SUMS.txt') -NoNewline

    if (-not $dev) {
    $wingetDir = Assert-Inside $wingetDir $Out
    if (Test-Path -LiteralPath $wingetDir) { Remove-Item -LiteralPath $wingetDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $wingetDir | Out-Null
    $header = '# Created by scripts/package.ps1 in https://github.com/' + $Repository
    $installers = foreach ($rid in $Runtime) {
        $name = Get-ReleaseZipName $Version $rid
        @"
  - Architecture: $($rid -replace '^win-', '')
    InstallerUrl: https://github.com/$Repository/releases/download/v$Version/$name
    InstallerSha256: $($sums[$name].ToUpperInvariant())
"@
    }
    @"
$header
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.$schema.schema.json

PackageIdentifier: $id
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: $schema
"@ | Set-Content -LiteralPath (Join-Path $wingetDir "$id.yaml")
    @"
$header
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.$schema.schema.json

PackageIdentifier: $id
PackageVersion: $Version
MinimumOSVersion: 10.0.22621.0
InstallerType: zip
Scope: user
NestedInstallerType: portable
NestedInstallerFiles:
  - RelativeFilePath: winshell.exe
# winshell.exe loads the DLLs next to it, so winget puts its folder on PATH instead of linking the exe. The command is
# then the exe's own name, winshell.
ArchiveBinariesDependOnPath: true
InstallModes:
  - silent
UpgradeBehavior: uninstallPrevious
ReleaseDate: $($releaseDay.ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture))
Installers:
$($installers -join "`n")
ManifestType: installer
ManifestVersion: $schema
"@ | Set-Content -LiteralPath (Join-Path $wingetDir "$id.installer.yaml")
    @"
$header
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.$schema.schema.json

PackageIdentifier: $id
PackageVersion: $Version
PackageLocale: en-US
Publisher: $publisherName
PublisherUrl: https://github.com/enpaze
PublisherSupportUrl: https://github.com/$Repository/issues
PackageName: $product
PackageUrl: https://github.com/$Repository
License: MIT
LicenseUrl: https://github.com/$Repository/blob/main/LICENSE
Copyright: Copyright (c) $publisherName
ShortDescription: A toolkit for writing a desktop shell for Windows 11.
Description: |-
  Winshell is a toolkit for writing your own desktop shell for Windows 11 as a .NET project. Installing it adds no
  taskbar: winshell builds and runs the shell you write.
Moniker: winshell
Tags:
  - desktop
  - shell
  - taskbar
  - windows-11
ReleaseNotesUrl: https://github.com/$Repository/releases/tag/v$Version
ManifestType: defaultLocale
ManifestVersion: $schema
"@ | Set-Content -LiteralPath (Join-Path $wingetDir "$id.locale.en-US.yaml")
    New-Zip $wingetDir (Join-Path $Out 'winget-manifests.zip') $releaseDay
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install.ps1'), (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination $Out

    $makeappx = Get-WindowsSdkTool makeappx.exe
    $packages = Assert-Inside (Join-Path $Out 'msix') $Out
    if (Test-Path -LiteralPath $packages) { Remove-Item -LiteralPath $packages -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $packages | Out-Null
    $template = Get-Content -LiteralPath (Join-Path $root 'packaging/msix/AppxManifest.xml') -Raw
    foreach ($rid in $Runtime) {
        $layout = Assert-Inside (Join-Path $Out "msix-layout/$rid") $Out
        if (Test-Path -LiteralPath $layout) { Remove-Item -LiteralPath $layout -Recurse -Force }
        Copy-Item -LiteralPath (Join-Path $publishRoot $rid) -Destination $layout -Recurse
        Copy-Item -LiteralPath (Join-Path $root 'packaging/msix/Assets') -Destination (Join-Path $layout 'Assets') -Recurse
        $manifest = Expand-StoreManifest $template $IdentityName $Publisher $PublisherDisplayName (Get-PackageVersion) ($rid -replace '^win-', '') $product
        Set-Content -LiteralPath (Join-Path $layout 'AppxManifest.xml') -Value $manifest -NoNewline
        & $makeappx pack /o /h SHA256 /d $layout /p (Join-Path $packages "winshell-$Version-$rid.msix") | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "makeappx pack for $rid failed with $LASTEXITCODE" }
        Remove-Item -LiteralPath $layout -Recurse -Force
    }
    Remove-Item -LiteralPath (Join-Path $Out 'msix-layout') -Recurse -Force
    & $makeappx bundle /o /bv (Get-PackageVersion) /d $packages /p (Join-Path $Out "winshell-$Version.msixbundle") | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makeappx bundle failed with $LASTEXITCODE" }
    }
}

if ($Verify) {
    $expectedVersion = if ($dev) { "$Version+$($Commit.Substring(0, 7))" } else { $Version }
    $assets = if ($dev) { Get-DevReleaseAssetName $Version $Runtime } else { Get-ReleaseAssetName $Version $Runtime }
    foreach ($asset in $assets) {
        if (-not (Test-Path -LiteralPath (Join-Path $Out $asset) -PathType Leaf)) { throw "No $asset in $Out" }
    }
    $listed = @{}
    foreach ($line in (Get-Content -LiteralPath (Join-Path $Out 'SHA256SUMS.txt'))) {
        $hash, $name = $line -split '  ', 2
        $listed[$name] = $hash
    }
    $machines = @{ 'win-x64' = 0x8664; 'win-arm64' = 0xAA64 }
    [xml] $signing = Get-Content -Raw -LiteralPath (Join-Path $root 'packaging/signpath/artifact-configuration.xml')
    $signedPaths = $signing.'artifact-configuration'.'zip-file'.'pe-file-set'.include.path
    $versionFile = Join-Path $Out 'version.json'
    if (-not $listed.ContainsKey('version.json') -or $listed['version.json'] -ne (Get-Sha256 $versionFile)) {
        throw 'version.json does not match its line in SHA256SUMS.txt'
    }
    $verifyRoot = Join-Path $Out 'verify'
    foreach ($rid in $Runtime) {
        $name = Get-ReleaseZipName $Version $rid
        $zip = Join-Path $Out $name
        if (-not (Test-Path -LiteralPath $zip)) { throw "No $name in $Out" }
        if (-not $listed.ContainsKey($name) -or $listed[$name] -ne (Get-Sha256 $zip)) { throw "$name does not match its line in SHA256SUMS.txt" }
        $dir = Assert-Inside (Join-Path $verifyRoot $rid) $Out
        if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
        [IO.Compression.ZipFile]::ExtractToDirectory($zip, $dir)
        $recorded = Get-Content -Raw -LiteralPath (Join-Path $dir 'version.json') | ConvertFrom-Json
        if ($recorded.version -cne $expectedVersion) { throw "$name records version $($recorded.version), not $expectedVersion" }
        Assert-WinshellPackage $dir $Version $name
        foreach ($binary in 'winshell.exe', 'hostfxr.dll', 'coreclr.dll', 'winshell.dll') {
            $path = Join-Path $dir $binary
            if (-not (Test-Path -LiteralPath $path)) { throw "$name has no $binary at its root" }
            $bytes = [IO.File]::ReadAllBytes($path)
            $machine = [BitConverter]::ToUInt16($bytes, (Get-PeLayout $bytes).Machine)
            if ($machine -ne $machines[$rid]) { throw "$binary in $name is for machine 0x$($machine.ToString('X4')), not $rid" }
        }
        foreach ($file in Get-ChildItem -LiteralPath $dir -Recurse -File -Include '*.dll', '*.exe') {
            $bytes = [IO.File]::ReadAllBytes($file.FullName)
            if ($bytes.Length -lt 0x40 -or $bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A) { continue }
            $machine = [BitConverter]::ToUInt16($bytes, (Get-PeLayout $bytes).Machine)
            if ($machine -ne $machines[$rid] -and $machine -ne 0x014C) {
                throw "$($file.Name) in $name is for machine 0x$($machine.ToString('X4')), not $rid"
            }
        }
        if ($rid -eq 'win-x64' -and [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'X64') {
            $reported = Assert-WinshellRuns (Join-Path $dir 'winshell.exe') $Version (Join-Path $verifyRoot 'doctor')
            Write-Output "$name runs: $reported"
            Write-Output "$name runs every winshell doctor check"
        }
        $ownBinaries = $signedPaths | Where-Object { $_.StartsWith("$rid/", [StringComparison]::Ordinal) } | ForEach-Object { $_.Substring($rid.Length + 1) }
        if (-not $ownBinaries) { throw "packaging/signpath/artifact-configuration.xml signs nothing in $rid" }
        Assert-WinshellSigned $dir $ownBinaries $name
        foreach ($binary in $ownBinaries) {
            $info = (Get-Item -LiteralPath (Join-Path $dir $binary)).VersionInfo
            if ($info.ProductName -cne $product -or $info.ProductVersion -notmatch "^$([regex]::Escape($Version))(\+|$)") {
                throw "$binary in $name is product '$($info.ProductName)' $($info.ProductVersion), not $product $Version"
            }
        }
        if ($Signed) {
            foreach ($binary in $ownBinaries) {
                $signature = Get-AuthenticodeSignature -LiteralPath (Join-Path $dir $binary)
                if ($signature.Status -ne 'Valid') { throw "$binary in $name is not signed: $($signature.StatusMessage)" }
                Write-Output "$binary in $name is signed by $($signature.SignerCertificate.Subject)"
            }
        }
        Remove-Item -LiteralPath $dir -Recurse -Force
    }
    if (Test-Path -LiteralPath $verifyRoot) { Remove-Item -LiteralPath $verifyRoot -Recurse -Force }

    if (-not $dev) {
    $installer = Get-Content -LiteralPath (Join-Path $wingetDir "$id.installer.yaml") -Raw
    foreach ($rid in $Runtime) {
        if ($installer -notmatch "(?m)^\s+InstallerSha256: $($listed[(Get-ReleaseZipName $Version $rid)].ToUpperInvariant())\r?$") {
            throw "The installer manifest does not carry the hash of the $rid zip"
        }
    }
    $tools = Join-Path $Out 'tools'
    $utils = Join-Path $tools ([IO.Path]::GetFileName($pins.wingetUtil.url))
    if (-not (Test-Path -LiteralPath $utils)) {
        New-Item -ItemType Directory -Force -Path $tools | Out-Null
        Invoke-WebRequest $pins.wingetUtil.url -OutFile $utils
    }
    if ((Get-Sha256 $utils) -ne $pins.wingetUtil.sha256) {
        Remove-Item -LiteralPath $utils
        throw 'The WinGetUtil package does not have the pinned hash'
    }
    $arch = if ([Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq 'Arm64') { 'win-arm64' } else { 'win-x64' }
    $dll = Join-Path $tools "$arch/WinGetUtil.dll"
    if (-not (Test-Path -LiteralPath $dll)) {
        New-Item -ItemType Directory -Force -Path (Split-Path $dll) | Out-Null
        $package = [IO.Compression.ZipFile]::OpenRead($utils)
        try {
            [IO.Compression.ZipFileExtensions]::ExtractToFile($package.GetEntry("runtimes/$arch/native/WinGetUtil.dll"), $dll)
        } finally { $package.Dispose() }
    }
    [Runtime.InteropServices.NativeLibrary]::Load($dll) | Out-Null
    if (-not ('WinshellPackage.WinGetUtil' -as [type])) {
        Add-Type -Namespace WinshellPackage -Name WinGetUtil -MemberDefinition @'
[DllImport("WinGetUtil.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
public static extern IntPtr WinGetValidateManifest(string manifestPath, [MarshalAs(UnmanagedType.U1)] out bool succeeded, [MarshalAs(UnmanagedType.BStr)] out string message);
'@
    }
    $ok = $false
    $message = ''
    [WinshellPackage.WinGetUtil]::WinGetValidateManifest($wingetDir, [ref] $ok, [ref] $message) | Out-Null
    if (-not $ok) { throw "The winget manifests do not validate: $message" }
    if ($message) { Write-Warning "winget manifest validation: $message" }

    $makeappx = Get-WindowsSdkTool makeappx.exe
    if (-not (Test-Path -LiteralPath (Join-Path $Out "winshell-$Version.msixbundle"))) { throw "No winshell-$Version.msixbundle in $Out" }
    foreach ($rid in $Runtime) {
        $dir = Assert-Inside (Join-Path $verifyRoot "msix-$rid") $Out
        if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
        & $makeappx unpack /o /p (Join-Path $Out "msix/winshell-$Version-$rid.msix") /d $dir | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "makeappx unpack for $rid failed with $LASTEXITCODE" }
        $identity = ([xml] (Get-Content -LiteralPath (Join-Path $dir 'AppxManifest.xml') -Raw)).Package.Identity
        $expected = "$IdentityName|$Publisher|$(Get-PackageVersion)|$($rid -replace '^win-', '')"
        $actual = "$($identity.Name)|$($identity.Publisher)|$($identity.Version)|$($identity.ProcessorArchitecture)"
        if ($actual -cne $expected) { throw "The $rid package's identity is $actual, not $expected" }
        Assert-WinshellPackage $dir $Version "The $rid package"
        foreach ($pair in @(@('winshell.exe', 3), @('winshellw.exe', 2))) {
            $bytes = [IO.File]::ReadAllBytes((Join-Path $dir $pair[0]))
            $subsystem = [BitConverter]::ToUInt16($bytes, (Get-PeLayout $bytes).Subsystem)
            if ($subsystem -ne $pair[1]) { throw "$($pair[0]) in the $rid package has subsystem $subsystem, not $($pair[1])" }
        }
        Remove-Item -LiteralPath $dir -Recurse -Force
    }
    if (Test-Path -LiteralPath $verifyRoot) { Remove-Item -LiteralPath $verifyRoot -Recurse -Force }
    }
    if ($dev) { Write-Output "Verified $($Runtime -join ' and ') $expectedVersion." }
    else { Write-Output "Verified $($Runtime -join ' and ') $Version, its winget manifests and its Store package $(Get-PackageVersion)." }
}
