#requires -Version 7.0
[CmdletBinding()]
param([switch] $VerifyNoChanges)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
$options = @()
if ($VerifyNoChanges) { $options += '--verify-no-changes' }

Push-Location $root
try {
    $paths = @(git -c core.quotepath=false ls-files --cached --others --exclude-standard -- src tests examples templates docs/samples scripts/generate-reference.cs scripts/reference-pages.cs scripts/render-logo.cs scripts/lib)
    if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate C# sources with Git.' }
    $sources = @($paths | Where-Object { $_ -like '*.cs' -and [IO.File]::Exists((Join-Path $root $_)) })
    if ($sources.Count -eq 0) { throw 'No C# sources were found.' }
    $sources | dotnet format whitespace . --folder --include - @options
    $formatExit = $LASTEXITCODE
}
finally {
    Pop-Location
}
exit $formatExit
