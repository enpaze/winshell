#requires -Version 7.0
[CmdletBinding()]
param(
    [string] $Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$banned = 'x' + 'unit'
$tracked = @(git -C $Root ls-files)
if ($LASTEXITCODE -ne 0) { throw "git ls-files failed in $Root." }
if ($tracked.Count -eq 0) { throw "$Root tracks no files, so there is nothing to check." }
$hits = @(git -c core.quotepath=off -C $Root grep --ignore-case --files-with-matches -I --fixed-strings -e $banned)
if ($LASTEXITCODE -gt 1) { throw "git grep failed in $Root." }
foreach ($hit in $hits) {
    Write-Output "${hit} names $banned. The tests run on MSTest 4: use [TestClass], [TestMethod], [DataRow] and Assert from Microsoft.VisualStudio.TestTools.UnitTesting, and no other test framework package."
}
if ($hits.Count -gt 0) { exit 1 }
Write-Output "No tracked file names $banned."
