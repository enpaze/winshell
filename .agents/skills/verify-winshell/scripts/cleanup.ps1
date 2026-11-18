#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string] $Session)

. (Join-Path $PSScriptRoot 'Session.ps1')

$null = Read-Session $Session
$problems = @(Stop-Session)
$problems | ForEach-Object { Write-Output "FAIL $_" }
Write-Output "Stopped every shell of $($script:Run.path). Evidence kept in $($script:Run.evidence)."
exit ([int] ($problems.Count -gt 0))
