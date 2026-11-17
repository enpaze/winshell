#requires -Version 7.0
param(
    [string] $Out = 'artifacts/packages',
    [string] $Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'lib/Release.ps1')
if (-not $Version) { $Version = Get-ReleaseVersion }
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw "Version must be a NuGet version, not '$Version'" }
$Out = [IO.Path]::GetFullPath($Out, $root)
New-Item -ItemType Directory -Force -Path $Out | Out-Null

dotnet pack (Join-Path $root 'src/Winshell.Package/Winshell.Package.csproj') -c Release -o $Out --nologo "-p:IncludeSourceRevisionInInformationalVersion=false" "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw "dotnet pack of Winshell failed with $LASTEXITCODE" }
