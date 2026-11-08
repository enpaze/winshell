#requires -Version 7.0
# Formats C# with the SDK formatter and compiles the template against the current libraries.
[CmdletBinding()]
param([switch] $VerifyNoChanges)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
$temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$session = Join-Path $temporary ('winshell-format-' + [guid]::NewGuid().ToString('N'))
$options = @()
if ($VerifyNoChanges) { $options += '--verify-no-changes' }

Push-Location $root
try {
    dotnet format winshell.slnx @options
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # Keep the package reference and version placeholder in the distributed template unchanged.
    $template = Join-Path $session 'template'
    [IO.Directory]::CreateDirectory($template) | Out-Null
    $paths = @(git ls-files --cached --others --exclude-standard -- templates/winshell)
    if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate template sources with Git.' }
    foreach ($path in $paths) {
        $source = Join-Path $root $path
        if (-not [IO.File]::Exists($source)) { continue }
        $destination = Join-Path $template $path.Substring('templates/winshell/'.Length)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        [IO.File]::Copy($source, $destination)
    }
    foreach ($name in @('.editorconfig', 'global.json')) {
        Copy-Item -LiteralPath (Join-Path $root $name) -Destination (Join-Path $session $name)
    }
    $projectPath = Join-Path $template 'MyShell.csproj'
    $project = [xml](Get-Content -LiteralPath $projectPath -Raw)
    $package = $project.SelectSingleNode('/Project/ItemGroup/PackageReference[@Include="Winshell"]')
    if ($null -eq $package) { throw 'The template no longer references the Winshell package.' }
    $reference = $project.CreateElement('ProjectReference')
    $reference.SetAttribute('Include', (Join-Path $root 'src/Winshell.Shell/Winshell.Shell.csproj'))
    $package.ParentNode.ReplaceChild($reference, $package) | Out-Null
    $project.Save($projectPath)

    dotnet build $projectPath -warnaserror --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet format $projectPath --no-restore @options
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if (-not $VerifyNoChanges) {
        foreach ($path in $paths | Where-Object { $_ -like '*.cs' }) {
            [IO.File]::Copy((Join-Path $template $path.Substring('templates/winshell/'.Length)), (Join-Path $root $path), $true)
        }
    }
}
finally {
    if (Test-Path -LiteralPath $session) {
        $resolved = (Resolve-Path -LiteralPath $session).ProviderPath
        if (-not $resolved.Equals($session, [StringComparison]::OrdinalIgnoreCase) -or
            -not $resolved.StartsWith($temporary.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove an unexpected temporary directory: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
    Pop-Location
}
exit 0
