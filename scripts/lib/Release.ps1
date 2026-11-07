# What a release ships and how its build is checked, for scripts/package.ps1, scripts/publish-release.ps1 and
# .github/workflows/package.yml. install.ps1 is fetched alone and can't dot-source this; Install.Tests.ps1 serves it a
# release named by Get-ReleaseZipName.

# The release in .release-please-manifest.json.
function Get-ReleaseVersion {
    $manifest = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) '.release-please-manifest.json'
    (Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).'.'
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

# Runs winshell.exe --version and returns what it printed, or stops unless that is $Version, with or without build
# metadata after a +.
function Assert-WinshellVersion([string] $Exe, [string] $Version) {
    $reported = & $Exe --version
    if ($LASTEXITCODE -ne 0 -or $reported -notmatch "^winshell $([regex]::Escape($Version))(\+|$)") {
        throw "$Exe reported '$reported', not version $Version"
    }
    $reported
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
