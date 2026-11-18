#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('features', 'starter', 'omarchy-style')][string] $Shell = 'features',
    [string] $Session,
    [string] $Winshell
)

. (Join-Path $PSScriptRoot 'Session.ps1')

Assert-NoOtherRun
$null = New-Session $Session $Winshell
try {
    $instance = Start-Shell $Shell
}
catch {
    $null = Stop-Session
    throw
}
Write-Output "Ready: the $Shell shell is running, pid $($instance.pid)."
Write-Output "Session: $($script:Run.path)"
Write-Output "Evidence: $($script:Run.evidence)"
