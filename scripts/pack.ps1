#requires -Version 7.0
# Packs the Winshell library a configuration references, and the dotnet new templates, into artifacts/packages/.
# Nothing here publishes. scripts/package.ps1 calls this to put the library in each release folder, and the Package
# tests call it to build a shell from the template. See docs/development.md.
param(
    [string] $Out = 'artifacts/packages',
    # Defaults to the release in .release-please-manifest.json.
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

# A package's version is the release, with no commit id after it: ProductVersion reads it.
dotnet pack (Join-Path $root 'src/Winshell.Package/Winshell.Package.csproj') -c Release -o $Out --nologo "-p:IncludeSourceRevisionInInformationalVersion=false" "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw "dotnet pack of Winshell failed with $LASTEXITCODE" }
dotnet pack (Join-Path $root 'templates/Winshell.Templates.csproj') -c Release -o $Out --nologo "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw "dotnet pack of Winshell.Templates failed with $LASTEXITCODE" }
