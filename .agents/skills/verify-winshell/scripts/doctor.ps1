#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string] $Session)

. (Join-Path $PSScriptRoot 'Session.ps1')

$failed = 0
function Write-Check([bool] $Passed, [string] $Text) {
    Write-Output "$(if ($Passed) { 'OK  ' } else { 'FAIL' }) $Text"
    if (-not $Passed) { $script:failed++ }
}

$null = Read-Session $Session
$exe = $script:Run.exe
Write-Check (Test-Path -LiteralPath $exe -PathType Leaf) "winshell is $exe"
if (Test-Path -LiteralPath $exe -PathType Leaf) {
    $build = Join-Path $script:Repo 'src\Winshell\bin\'
    $version = Invoke-Winshell @('--version') ([TimeSpan]::FromSeconds(30))
    Write-Check ($version.Exit -eq 0) "winshell --version: $($version.Text)"
    if ($exe.StartsWith($build, [StringComparison]::OrdinalIgnoreCase)) {
        $began = [DateTime]::Parse($script:Run.started, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
        $sources = 'src', 'templates', 'examples', '.agents/skills/verify-winshell/shell' | ForEach-Object { Join-Path $script:Repo $_ }
        $newest = Get-ChildItem -LiteralPath $sources -Recurse -File -Include '*.cs', '*.csproj', '*.xaml', '*.props', '*.targets' |
            Where-Object FullName -NotMatch '\\(bin|obj)\\' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $stale = $null -ne $newest -and $newest.LastWriteTime -gt $began
        Write-Check (-not $stale) "no source changed since the session began$(if ($stale) { "; $($newest.FullName) did, so clean up and launch again" })"
    }
    $folder = $script:Run.folder
    Write-Check ($null -ne $folder) "the session has a shell$(if ($folder) { ": $($script:Run.shell) in $folder" } else { '; launch.ps1 starts one' })"
    if ($folder) {
        $instance = Get-ShellInstance $folder
        Write-Check ($null -ne $instance) "winshell list -j lists the $($script:Run.shell) shell$(if ($instance) { ", pid $($instance.pid)" })"
        if ($instance) {
            if ([string] $instance.pid -ne [string] $script:Run.pid) {
                Write-Output "NOTE the shell's pid is now $($instance.pid), not $($script:Run.pid): a reload or run replaced it."
            }
            $process = Get-Process -Id $instance.pid -ErrorAction SilentlyContinue
            $path = if ($process) { Get-ProcessPath $process } else { $null }
            Write-Check ($null -ne $path -and $path.StartsWith($script:Run.config + '\', [StringComparison]::OrdinalIgnoreCase)) "pid $($instance.pid) runs from this session: $path"
            $tree = Get-Tree $instance.pid
            $kinds = @($tree.Windows | ForEach-Object { "$($_.window) $($_.kind) $(Get-Field (Get-Field $_ 'content') 'type')" })
            Write-Check (Test-Ready $tree) "winshell inspect --pid $($instance.pid) tree shows $($tree.Windows.Count) windows, each with content: $($kinds -join '; ')"
        }
    }
}
$others = @(Get-OtherRuns)
Write-Check ($others.Count -eq 0) "no other verification run is live$(if ($others) { ': ' + (($others | ForEach-Object { "$($_.Name) $($_.Id) from $($_.Session)" }) -join ', ') })"
exit ([int] ($failed -gt 0))
