#requires -Version 7.0
# Claude Code reads the repo's skills from .claude/skills, and the other agents from .agents/skills, so the repo commits
# both, the same files. This makes .claude/skills a copy of .agents/skills:
#
#   pwsh scripts/copy-skills.ps1
#
# With -VerifyNoChanges it changes nothing: it prints each file the two folders do not share byte for byte, and exits 1
# if there is one. CI runs the check on every pull request that is not a draft.
param([switch] $VerifyNoChanges)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root '.agents/skills'
$copy = Join-Path $root '.claude/skills'

if (-not $VerifyNoChanges) {
    if (Test-Path -LiteralPath $copy) {
        # The skills tool links each skill it updates into .claude/skills. Remove the links first, without following
        # them into .agents/skills: a recursive delete can't remove a junction on Windows.
        Get-ChildItem -LiteralPath $copy -Directory -Force | Where-Object LinkType | ForEach-Object { $_.Delete() }
        Remove-Item -LiteralPath $copy -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $copy) | Out-Null
    Copy-Item -LiteralPath $source -Destination $copy -Recurse -Force
    return
}

# Each file under a folder by its path inside it. A link is not a file, so a linked skill shows up as missing.
function Get-File {
    param([string] $Folder)
    $files = @{}
    if (Test-Path -LiteralPath $Folder) {
        foreach ($file in Get-ChildItem -LiteralPath $Folder -Recurse -File -Force) {
            $files[[IO.Path]::GetRelativePath($Folder, $file.FullName).Replace('\', '/')] = $file.FullName
        }
    }
    $files
}

$sourceFiles = Get-File $source
$copyFiles = Get-File $copy
$different = @(
    foreach ($path in @($sourceFiles.Keys) + @($copyFiles.Keys) | Sort-Object -Unique) {
        if (-not $sourceFiles.ContainsKey($path)) { "only in .claude/skills: $path" }
        elseif (-not $copyFiles.ContainsKey($path)) { "only in .agents/skills: $path" }
        elseif ((Get-FileHash -LiteralPath $sourceFiles[$path]).Hash -ne (Get-FileHash -LiteralPath $copyFiles[$path]).Hash) { "differs: $path" }
    }
)
if ($different.Count -gt 0) {
    $different | Write-Output
    Write-Output 'Run pwsh scripts/copy-skills.ps1 to make .claude/skills a copy of .agents/skills.'
    exit 1
}
