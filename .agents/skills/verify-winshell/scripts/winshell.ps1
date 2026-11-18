#requires -Version 7.0
param([string] $Session)

. (Join-Path $PSScriptRoot 'Session.ps1')

$null = Read-Session $Session
$saved = @{ WINSHELL_CONFIG_DIR = $env:WINSHELL_CONFIG_DIR; WINSHELL_LOCAL_DIR = $env:WINSHELL_LOCAL_DIR }
try {
    $env:WINSHELL_CONFIG_DIR = $script:Run.config
    $env:WINSHELL_LOCAL_DIR = $script:Run.local
    & $script:Run.exe @args
    $code = $LASTEXITCODE
}
finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
}
exit $code
