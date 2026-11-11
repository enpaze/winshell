# What a release ships and how its build is checked, for scripts/package.ps1, scripts/pack.ps1,
# scripts/publish-release.ps1 and .github/workflows/package.yml. install.ps1 is a release asset fetched on its own
# and can't dot-source this; Install.Tests.ps1 serves it a release named by Get-ReleaseZipName, and Release.Tests.ps1
# ties its shortcut description to Get-ProductName. SignPath gets that same name as its product parameter.

function Get-RepoRoot { Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }

# The release in .release-please-manifest.json: its "." property, not another number in the file.
function Get-ReleaseVersion {
    $manifest = Join-Path (Get-RepoRoot) '.release-please-manifest.json'
    (Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).'.'
}

# The product name Directory.Build.props stamps into every binary.
function Get-ProductName {
    $props = Join-Path (Get-RepoRoot) 'Directory.Build.props'
    $match = [regex]::Match((Get-Content -LiteralPath $props -Raw), '<Product>([^<]+)</Product>')
    if (-not $match.Success) { throw 'Directory.Build.props does not set Product.' }
    $match.Groups[1].Value
}

# Fills the $(...) values in the Store manifest.
function Expand-StoreManifest(
    [string] $Template,
    [string] $IdentityName,
    [string] $Publisher,
    [string] $PublisherDisplayName,
    [string] $PackageVersion,
    [string] $Architecture,
    [string] $ProductName
) {
    $manifest = $Template.Replace('$(IdentityName)', [Security.SecurityElement]::Escape($IdentityName)).
        Replace('$(Publisher)', [Security.SecurityElement]::Escape($Publisher)).
        Replace('$(PublisherDisplayName)', [Security.SecurityElement]::Escape($PublisherDisplayName)).
        Replace('$(PackageVersion)', $PackageVersion).
        Replace('$(Architecture)', $Architecture).
        Replace('$(ProductName)', [Security.SecurityElement]::Escape($ProductName))
    $manifest
}

# The Store package identity used until the Partner Center variables are set.
function Get-StoreTestIdentity {
    @{ Name = 'Winshell.Test'; Publisher = 'CN=Winshell Test' }
}

function Get-ReleaseZipName([string] $Version, [string] $Runtime) { "winshell-$Version-$Runtime.zip" }

# Every file a release uploads. A local build of fewer runtimes has fewer zips.
function Get-ReleaseAssetName([string] $Version, [string[]] $Runtime = @('win-x64', 'win-arm64')) {
    @($Runtime | ForEach-Object { Get-ReleaseZipName $Version $_ }) +
    @('SHA256SUMS.txt', 'winget-manifests.zip', 'install.ps1', 'uninstall.ps1')
}

# A tool from the newest Windows SDK that has it, such as makeappx.exe or signtool.exe.
function Get-WindowsSdkTool([string] $Name) {
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
    $found = Get-ChildItem -LiteralPath $kits -Directory -Filter '10.*' -ErrorAction SilentlyContinue |
        Sort-Object { [version] $_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName "x64/$Name" } |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1
    if (-not $found) { throw "$Name is not in $kits. Install the Windows SDK." }
    $found
}

# The start of the sentence Doctor.Guard returns when a check could not run. The release check uses that sentence,
# not a second prefix.
function Get-DoctorGuardPrefix {
    $doctor = Join-Path (Get-RepoRoot) 'src/Winshell.Services/Diagnostics/Doctor.cs'
    $match = [regex]::Match(
        (Get-Content -LiteralPath $doctor -Raw),
        '\$"(Could not check: )\{e\.GetType\(\)\.Name\}: \{e\.Message\}"')
    if (-not $match.Success) { throw 'Doctor.Guard does not return its could-not-check sentence.' }
    $match.Groups[1].Value
}

function Test-DoctorCouldNotCheck([string] $Message) {
    $Message -and $Message.StartsWith((Get-DoctorGuardPrefix), [StringComparison]::Ordinal)
}

# Drops the files src/Winshell names in WinshellReleaseRemoved, and folders that leaves empty. Only the release publish
# calls this. A plain `dotnet publish src/Winshell` keeps symbols and helpers.
function Remove-ReleaseFile([string] $Dir) {
    $project = Join-Path (Get-RepoRoot) 'src/Winshell/Winshell.csproj'
    $match = [regex]::Match(
        (Get-Content -LiteralPath $project -Raw),
        '<WinshellReleaseRemoved>([^<]+)</WinshellReleaseRemoved>')
    if (-not $match.Success) { throw 'src/Winshell/Winshell.csproj lists no files a release removes.' }
    $patterns = @($match.Groups[1].Value.Split(';') | ForEach-Object Trim | Where-Object { $_ })
    Get-ChildItem -LiteralPath $Dir -Recurse -File | Where-Object {
        $name = $_.Name
        @($patterns | Where-Object { $name -like $_ }).Count -gt 0
    } | Remove-Item -Force
    Get-ChildItem -LiteralPath $Dir -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending | Where-Object {
        -not (Get-ChildItem -LiteralPath $_.FullName -Force | Select-Object -First 1)
    } | Remove-Item -Force
}

# Runs winshell doctor from an unpacked release, on folders of its own. A library a check uses that the release left
# out makes that check return Doctor.Guard's sentence, and fails only here, because tests run the build and not the zip.
function Assert-WinshellDoctor([string] $Exe, [string] $Directory) {
    $config = Join-Path $Directory 'config'
    $local = Join-Path $Directory 'local'
    New-Item -ItemType Directory -Force -Path $config, $local | Out-Null
    $env:WINSHELL_CONFIG_DIR = $config
    $env:WINSHELL_LOCAL_DIR = $local
    try {
        $report = & $Exe doctor --json
        # 1 is a check that failed on this PC, which the release cannot help.
        if ($LASTEXITCODE -notin 0, 1) { throw "winshell doctor exited with $LASTEXITCODE." }
        $checks = @(($report -join "`n" | ConvertFrom-Json).checks)
        if ($checks.Count -eq 0) { throw 'winshell doctor printed no checks.' }
        $broken = @($checks | Where-Object { Test-DoctorCouldNotCheck $_.message })
        if ($broken.Count -gt 0) { throw "winshell doctor could not run its $($broken[0].id) check: $($broken[0].message)" }
    } finally {
        Remove-Item -LiteralPath Env:WINSHELL_CONFIG_DIR, Env:WINSHELL_LOCAL_DIR
    }
}

# Runs winshell.exe --version, and, when $DoctorDirectory is set, every winshell doctor check. The x64 verify passes a
# directory. The arm64 job does not: running doctor there would be a new check.
function Assert-WinshellRuns([string] $Exe, [string] $Version, [string] $DoctorDirectory) {
    $reported = Assert-WinshellVersion $Exe $Version
    if ($DoctorDirectory) { Assert-WinshellDoctor $Exe $DoctorDirectory }
    $reported
}

# Runs winshell.exe --version and returns what it printed, or stops unless that is $Version, with or without build
# metadata after a +.
function Assert-WinshellVersion([string] $Exe, [string] $Version) {
    $reported = & $Exe --version
    if ($LASTEXITCODE -ne 0 -or $reported -notmatch "^winshell $([regex]::Escape($Version))(\+|$)") {
        throw "$Exe reported '$reported', not version $Version"
    }
    $reported
}

# Stops unless the packages folder in $Dir, an unpacked release, holds the Winshell package of $Version and nothing else.
# $What names the release in the message.
function Assert-WinshellPackage([string] $Dir, [string] $Version, [string] $What) {
    $expected = "Winshell.$Version.nupkg"
    $found = @(Get-ChildItem -LiteralPath (Join-Path $Dir 'packages') -File -ErrorAction SilentlyContinue | ForEach-Object Name)
    if (($found -join ', ') -cne $expected) {
        $has = if ($found) { "packages/$($found -join ', packages/')" } else { 'no package' }
        throw "$What has $has, not packages/$expected"
    }
}

# A zip of a folder whose bytes depend only on the files and the day: entries sorted by path, each dated that day.
function New-Zip([string] $dir, [string] $zip, [datetime] $day) {
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    # Ordinal, not the current culture: lt-LT orders "y" after U+012F, i with ogonek. A dictionary keeps names that differ only in case.
    $paths = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($file in Get-ChildItem -LiteralPath $dir -File -Recurse) {
        $paths[[IO.Path]::GetRelativePath($dir, $file.FullName).Replace('\', '/')] = $file.FullName
    }
    $names = [string[]]::new($paths.Count)
    $paths.Keys.CopyTo($names, 0)
    [Array]::Sort($names, [StringComparer]::Ordinal)
    $stream = [IO.File]::Open($zip, [IO.FileMode]::CreateNew)
    try {
        $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($name in $names) {
                $entry = $archive.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = [DateTimeOffset]::new($day, [TimeSpan]::Zero)
                $to = $entry.Open()
                try {
                    $from = [IO.File]::OpenRead($paths[$name])
                    try { $from.CopyTo($to) } finally { $from.Dispose() }
                } finally { $to.Dispose() }
            }
        } finally { $archive.Dispose() }
    } finally { $stream.Dispose() }
}

# Where a PE file's Machine and Subsystem fields are. e_lfanew, at 0x3C, points at the "PE\0\0" signature; the 20-byte
# COFF header follows it, and Subsystem is 68 bytes into the optional header after that.
function Get-PeLayout([byte[]] $bytes) {
    $coff = [BitConverter]::ToInt32($bytes, 0x3C) + 4
    @{ Machine = $coff; Subsystem = $coff + 20 + 68 }
}
