$script:Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

function Get-ReleaseVersion {
    $manifest = Join-Path $script:Root '.release-please-manifest.json'
    (Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).'.'
}

function Get-ProductName {
    $props = Join-Path $script:Root 'Directory.Build.props'
    $match = [regex]::Match((Get-Content -LiteralPath $props -Raw), '<Product>([^<]+)</Product>')
    if (-not $match.Success) { throw 'Directory.Build.props does not set Product.' }
    $match.Groups[1].Value
}

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

function Get-StoreTestIdentity {
    @{ Name = 'Winshell.Test'; Publisher = 'CN=Winshell Test' }
}

function Get-ReleaseZipName([string] $Version, [string] $Runtime) { "winshell-$Version-$Runtime.zip" }

function Get-ReleaseAssetName([string] $Version, [string[]] $Runtime = @('win-x64', 'win-arm64')) {
    @($Runtime | ForEach-Object { Get-ReleaseZipName $Version $_ }) +
    @('SHA256SUMS.txt', 'version.json', 'winget-manifests.zip', 'install.ps1', 'uninstall.ps1')
}

function Get-DevReleaseAssetName([string] $Version, [string[]] $Runtime = @('win-x64', 'win-arm64')) {
    @($Runtime | ForEach-Object { Get-ReleaseZipName $Version $_ }) + @('SHA256SUMS.txt', 'version.json')
}

function Write-ReleaseVersionFile([string] $Path, [string] $Version, [string] $Commit, [datetimeoffset] $Date) {
    if ($Commit -notmatch '^[0-9a-fA-F]{40}$') { throw "The release commit must be 40 hex digits, not '$Commit'." }
    $json = [ordered]@{
        version = $Version
        commit = $Commit.ToLowerInvariant()
        date = $Date.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ', [Globalization.CultureInfo]::InvariantCulture)
    } | ConvertTo-Json -Compress
    $directory = Split-Path -Parent $Path
    if ($directory -and -not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
    [IO.File]::WriteAllText($Path, $json + "`n")
}

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

function Test-DoctorCouldNotCheck([string] $Message) {
    if (-not $Message) { return $false }
    $doctor = Join-Path $script:Root 'src/Winshell.Services/Diagnostics/Doctor.cs'
    $match = [regex]::Match(
        (Get-Content -LiteralPath $doctor -Raw),
        '\$"(Could not check: )\{e\.GetType\(\)\.Name\}: \{e\.Message\}"')
    if (-not $match.Success) { throw 'Doctor.Guard does not return its could-not-check sentence.' }
    $Message.StartsWith($match.Groups[1].Value, [StringComparison]::Ordinal)
}

function Remove-ReleaseFile([string] $Dir) {
    $project = Join-Path $script:Root 'src/Winshell/Winshell.csproj'
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

function Assert-WinshellRuns([string] $Exe, [string] $Version, [string] $DoctorDirectory) {
    $reported = Assert-WinshellVersion $Exe $Version
    if (-not $DoctorDirectory) { return $reported }
    $config = Join-Path $DoctorDirectory 'config'
    $local = Join-Path $DoctorDirectory 'local'
    New-Item -ItemType Directory -Force -Path $config, $local | Out-Null
    $savedConfig = $env:WINSHELL_CONFIG_DIR
    $savedLocal = $env:WINSHELL_LOCAL_DIR
    $env:WINSHELL_CONFIG_DIR = $config
    $env:WINSHELL_LOCAL_DIR = $local
    try {
        $report = & $Exe doctor --json
        if ($LASTEXITCODE -notin 0, 1) { throw "winshell doctor exited with $LASTEXITCODE." }
        $checks = @(($report -join "`n" | ConvertFrom-Json).checks)
        if ($checks.Count -eq 0) { throw 'winshell doctor printed no checks.' }
        $broken = @($checks | Where-Object { Test-DoctorCouldNotCheck $_.message })
        if ($broken.Count -gt 0) { throw "winshell doctor could not run its $($broken[0].id) check: $($broken[0].message)" }
    } finally {
        if ($null -eq $savedConfig) { Remove-Item Env:WINSHELL_CONFIG_DIR -ErrorAction SilentlyContinue }
        else { $env:WINSHELL_CONFIG_DIR = $savedConfig }
        if ($null -eq $savedLocal) { Remove-Item Env:WINSHELL_LOCAL_DIR -ErrorAction SilentlyContinue }
        else { $env:WINSHELL_LOCAL_DIR = $savedLocal }
    }
    $reported
}

function Assert-WinshellVersion([string] $Exe, [string] $Version) {
    $reported = & $Exe --version
    if ($LASTEXITCODE -ne 0 -or $reported -notmatch "^winshell $([regex]::Escape($Version))(\+|$)") {
        throw "$Exe reported '$reported', not version $Version"
    }
    $reported
}

function Assert-WinshellPackage([string] $Dir, [string] $Version, [string] $What) {
    $expected = "Winshell.$Version.nupkg"
    $found = @(Get-ChildItem -LiteralPath (Join-Path $Dir 'packages') -File -ErrorAction SilentlyContinue | ForEach-Object Name)
    if (($found -join ', ') -cne $expected) {
        $has = if ($found) { "packages/$($found -join ', packages/')" } else { 'no package' }
        throw "$What has $has, not packages/$expected"
    }
}

function Assert-WinshellSigned([string] $Dir, [string[]] $Signed, [string] $What) {
    $carried = @(Get-ChildItem -LiteralPath $Dir -Recurse -File -Filter 'Winshell*.dll' | ForEach-Object { [IO.Path]::GetRelativePath($Dir, $_.FullName).Replace('\', '/') })
    if ($carried -contains 'Winshell.Generators.dll') {
        throw "$What carries Winshell.Generators.dll, an analyzer only the package ships. Reference it with OutputItemType=`"Analyzer`" ReferenceOutputAssembly=`"false`"."
    }
    $unsigned = @($carried | Where-Object { $Signed -cnotcontains $_ })
    if ($unsigned) {
        throw "$What carries $($unsigned -join ', ') unsigned. Add each to packaging/signpath/artifact-configuration.xml for every runtime."
    }
}

function New-Zip([string] $dir, [string] $zip, [datetime] $day) {
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
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

function Get-PeLayout([byte[]] $bytes) {
    $coff = [BitConverter]::ToInt32($bytes, 0x3C) + 4
    @{ Machine = $coff; Subsystem = $coff + 20 + 68 }
}
