#requires -Version 7.0
# Compiles template sources against the current libraries without changing the distributed project.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
$temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$session = Join-Path $temporary ('winshell-template-' + [guid]::NewGuid().ToString('N'))

Push-Location $root
try {
    $template = Join-Path $session 'template'
    [IO.Directory]::CreateDirectory($template) | Out-Null
    $paths = @(git -c core.quotepath=false ls-files --cached --others --exclude-standard -- templates/winshell)
    if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate template sources with Git.' }
    foreach ($path in $paths) {
        $source = Join-Path $root $path
        if (-not [IO.File]::Exists($source)) { continue }
        $destination = Join-Path $template $path.Substring('templates/winshell/'.Length)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        [IO.File]::Copy($source, $destination)
    }
    # Apply the repository's style, analyzer and unsafe-code policy to template compilation too.
    foreach ($name in @('.editorconfig', 'global.json', 'Directory.Build.props', 'Directory.Build.targets')) {
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
