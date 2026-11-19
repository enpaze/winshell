#requires -Version 7.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:Skill = Split-Path -Parent $PSScriptRoot
$script:Repo = [IO.Path]::GetFullPath((Join-Path $script:Skill '../../..'))
$script:Shells = [ordered]@{
    none = $null
    features = '.agents/skills/verify-winshell/shell'
    starter = 'templates/winshell'
    'omarchy-style' = 'examples/omarchy-style'
}
$script:CommandTimeout = [TimeSpan]::FromMinutes(10)
$script:Run = $null

function Get-Field($Object, [string] $Name, $Default = $null) {
    if ($null -ne $Object -and $Object.PSObject.Properties.Name -contains $Name) { return $Object.$Name }
    $Default
}

function Get-ProcessPath([Diagnostics.Process] $Process) {
    try { $Process.Path } catch { $null }
}

function Get-OtherRuns {
    $own = if ($script:Run) { $script:Run.path + '\' } else { $null }
    @(Get-Process | ForEach-Object {
            $path = Get-ProcessPath $_
            if ($path -and $path -match '^(.*\\winshell-verify(\\[^\\]+)?)\\shells\\' -and
                (-not $own -or -not $path.StartsWith($own, [StringComparison]::OrdinalIgnoreCase))) {
                [pscustomobject]@{ Id = $_.Id; Name = $_.ProcessName; Session = $Matches[1] }
            }
        })
}

function Assert-NoOtherRun {
    $others = @(Get-OtherRuns)
    if ($others.Count -gt 0) {
        $lines = $others | ForEach-Object { "  $($_.Name) $($_.Id) from $($_.Session)" }
        throw "Another verification run is live on this desktop, and one run at a time may drive it:`n$($lines -join "`n")`nWait for it, or end a run you started with cleanup.ps1 -Session <its folder>."
    }
}

function Build-Winshell {
    dotnet build (Join-Path $script:Repo 'src/Winshell/Winshell.csproj') -c Release --nologo -v q | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'src/Winshell did not build.' }
    @(Get-ChildItem -LiteralPath (Join-Path $script:Repo 'src/Winshell/bin/Release') -Recurse -Filter 'winshell.exe')[0].FullName
}

function Save-Session {
    $script:Run | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $script:Run.path 'session.json') -Encoding utf8NoBOM
}

function New-Session([string] $Path, [string] $Winshell) {
    if (-not $Path) {
        $stamp = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6)
        $Path = Join-Path ([IO.Path]::GetTempPath()) "winshell-verify\$stamp"
    }
    $Path = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if ((Test-Path -LiteralPath $Path) -and @(Get-ChildItem -LiteralPath $Path -Force).Count -gt 0) {
        throw "$Path is not empty. Name a new folder, or leave -Session out for a fresh one under $([IO.Path]::GetTempPath())winshell-verify."
    }
    $exe = if ($Winshell) { (Resolve-Path -LiteralPath $Winshell).ProviderPath } else { Build-Winshell }
    $script:Run = [pscustomobject]@{
        path = $Path
        shells = Join-Path $Path 'shells'
        local = Join-Path $Path 'local'
        evidence = Join-Path $Path 'evidence'
        exe = $exe
        started = [DateTime]::Now.ToString('o')
        shell = $null
        folder = $null
        pid = $null
    }
    foreach ($folder in $script:Run.shells, $script:Run.local, $script:Run.evidence) {
        [IO.Directory]::CreateDirectory($folder) | Out-Null
    }
    foreach ($name in '.editorconfig', 'global.json', 'Directory.Build.props', 'Directory.Build.targets') {
        Copy-Item -LiteralPath (Join-Path $script:Repo $name) -Destination (Join-Path $script:Run.shells $name)
    }
    Save-Session
    $script:Run
}

function Read-Session([string] $Path) {
    $file = Join-Path ([IO.Path]::GetFullPath($Path)) 'session.json'
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "$Path has no session.json. Pass the folder launch.ps1 or drive.ps1 printed."
    }
    $script:Run = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    $script:Run
}

function Start-Winshell([string[]] $Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($script:Run.exe)
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $start.Environment['WINSHELL_SHELLS_DIR'] = $script:Run.shells
    $start.Environment['WINSHELL_LOCAL_DIR'] = $script:Run.local
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
    $start.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
    $process = [Diagnostics.Process]::Start($start)
    [pscustomobject]@{
        Process = $process
        Output = $process.StandardOutput.ReadToEndAsync()
        Error = $process.StandardError.ReadToEndAsync()
    }
}

function Stop-Started($Started) {
    if (-not $Started.Process.HasExited) {
        try { $Started.Process.Kill($true) } catch [InvalidOperationException] { }
        $null = $Started.Process.WaitForExit(10000)
    }
}

function Read-Started($Started) {
    $null = $Started.Process.WaitForExit(10000)
    $text = ''
    foreach ($task in $Started.Output, $Started.Error) {
        if ($task.Wait(5000)) { $text += $task.Result }
    }
    $text.TrimEnd()
}

function Invoke-Winshell([string[]] $Arguments, [TimeSpan] $Timeout = $script:CommandTimeout) {
    $started = Start-Winshell $Arguments
    if (-not $started.Process.WaitForExit([int] $Timeout.TotalMilliseconds)) {
        Stop-Started $started
        return [pscustomobject]@{ Exit = $null; Text = (Read-Started $started) + "`n(still running after $($Timeout.TotalSeconds) s)" }
    }
    $text = Read-Started $started
    [pscustomobject]@{ Exit = $started.Process.ExitCode; Text = $text }
}

function Get-Instances {
    $result = Invoke-Winshell @('list', '-j') ([TimeSpan]::FromSeconds(30))
    if ($result.Exit -ne 0) { throw "winshell list -j failed: $($result.Text)" }
    @($result.Text | ConvertFrom-Json)
}

function Get-ShellInstance([string] $Folder) {
    $prefix = (Join-Path $Folder 'bin\shell') + '\'
    @(Get-Instances | Where-Object { $_.program.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) }) |
        Select-Object -First 1
}

function Get-ShellProcesses {
    $exe = $script:Run.exe
    $shells = $script:Run.shells + '\'
    @(Get-Process | Where-Object {
            $path = Get-ProcessPath $_
            $path -and ($path.StartsWith($shells, [StringComparison]::OrdinalIgnoreCase) -or ($path -eq $exe -and $_.Id -ne $PID))
        })
}

function Wait-ShellInstance([string] $Folder, [double] $Seconds) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    do {
        if ($instance = Get-ShellInstance $Folder) { return $instance }
        if (@(Get-ShellProcesses).Count -eq 0) { return $null }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    $null
}

function Get-Tree([int] $ProcessId) {
    $tree = Invoke-Winshell @('inspect', '--pid', [string] $ProcessId, 'tree') ([TimeSpan]::FromSeconds(30))
    $windows = @(if ($tree.Exit -eq 0) { $tree.Text | ConvertFrom-Json -Depth 1024 })
    [pscustomobject]@{ Exit = $tree.Exit; Text = $tree.Text; Windows = $windows }
}

function Test-Ready($Tree) {
    $empty = @($Tree.Windows | Where-Object { -not (Get-Field (Get-Field $_ 'content') 'children') })
    $Tree.Windows.Count -gt 0 -and $empty.Count -eq 0
}

function Start-ShellFolder([string] $Folder) {
    $result = Invoke-Winshell @('run', '-d', '-p', $Folder)
    if ($result.Exit -ne 0) { throw "winshell run -d -p $Folder exited with $($result.Exit): $($result.Text)" }
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($instance = Get-ShellInstance $Folder) {
            if (Test-Ready (Get-Tree $instance.pid)) { return $instance }
        }
        Start-Sleep -Milliseconds 250
    }
    throw "winshell run -d -p $Folder returned, but the shell never showed a window in winshell list -j and inspect tree."
}

function Copy-Shell([string] $Name) {
    $source = $script:Shells[$Name]
    $folder = Join-Path $script:Run.shells $Name
    $paths = @(git -C $script:Repo -c core.quotepath=false ls-files --cached --others --exclude-standard -- $source)
    if ($LASTEXITCODE -ne 0) { throw "Could not list the files of $source with Git." }
    foreach ($path in $paths) {
        $from = Join-Path $script:Repo $path
        if (-not [IO.File]::Exists($from)) { continue }
        $to = Join-Path $folder $path.Substring($source.Length + 1)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to)) | Out-Null
        [IO.File]::Copy($from, $to, $true)
    }
    $projectPath = @(Get-ChildItem -LiteralPath $folder -Filter '*.csproj')[0].FullName
    $project = [xml] (Get-Content -LiteralPath $projectPath -Raw)
    $sourceFolder = Join-Path $script:Repo $source
    foreach ($node in @($project.SelectNodes('/Project/Import[@Project]'))) {
        $node.SetAttribute('Project', [IO.Path]::GetFullPath((Join-Path $sourceFolder $node.Project)))
    }
    foreach ($node in @($project.SelectNodes('/Project/ItemGroup/ProjectReference[@Include]'))) {
        $node.SetAttribute('Include', [IO.Path]::GetFullPath((Join-Path $sourceFolder $node.Include)))
    }
    $package = $project.SelectSingleNode('/Project/ItemGroup/PackageReference[@Include="Winshell"]')
    if ($null -ne $package) {
        $reference = $project.CreateElement('ProjectReference')
        $reference.SetAttribute('Include', (Join-Path $script:Repo 'src/Winshell.Shell/Winshell.Shell.csproj'))
        $package.ParentNode.ReplaceChild($reference, $package) | Out-Null
        $import = $project.CreateElement('Import')
        $import.SetAttribute('Project', (Join-Path $script:Repo 'src/Winshell.Package/buildTransitive/Winshell.props'))
        $project.DocumentElement.PrependChild($import) | Out-Null
    }
    $definitions = $project.CreateElement('ItemDefinitionGroup')
    $definition = $project.CreateElement('ProjectReference')
    $remove = $project.CreateElement('GlobalPropertiesToRemove')
    $remove.InnerText = 'OutputPath'
    $definition.AppendChild($remove) | Out-Null
    $definitions.AppendChild($definition) | Out-Null
    $project.DocumentElement.AppendChild($definitions) | Out-Null
    $project.Save($projectPath)
    $folder
}

function Start-Shell([string] $Name) {
    $folder = if (Test-Path -LiteralPath (Join-Path $script:Run.shells $Name)) { Join-Path $script:Run.shells $Name } else { Copy-Shell $Name }
    $instance = Start-ShellFolder $folder
    $script:Run.shell = $Name
    $script:Run.folder = $folder
    $script:Run.pid = $instance.pid
    Save-Session
    $instance
}

function Stop-Shells {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        foreach ($instance in Get-Instances) {
            $result = Invoke-Winshell @('kill', '--pid', [string] $instance.pid) ([TimeSpan]::FromSeconds(30))
            if ($result.Exit -ne 0) { $null = Invoke-Winshell @('kill', '--force', '--pid', [string] $instance.pid) ([TimeSpan]::FromSeconds(30)) }
        }
        if (@(Get-ShellProcesses).Count -eq 0) { return }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
}

function Stop-Session {
    $problems = [Collections.Generic.List[string]]::new()
    $session = $script:Run
    $began = [DateTime]::Parse($session.started, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
    if (Test-Path -LiteralPath $session.exe) {
        Stop-Shells
        foreach ($instance in @(Get-Instances)) { $problems.Add("instance $($instance.id) was still listed") }
    }
    $own = $session.path + '\'
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $stray = @(Get-Process | Where-Object {
                $path = Get-ProcessPath $_
                $path -and ($path.StartsWith($own, [StringComparison]::OrdinalIgnoreCase) -or
                    ($path -eq $session.exe -and $_.StartTime -ge $began -and $_.Id -ne $PID))
            })
        if ($stray.Count -eq 0) { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    foreach ($process in $stray) {
        $problems.Add("$($process.ProcessName) $($process.Id) was still running, so it was ended")
        $process.Kill($true)
        $null = $process.WaitForExit(10000)
    }
    $logs = Join-Path $session.local 'run'
    if (Test-Path -LiteralPath $logs) {
        Copy-Item -LiteralPath $logs -Destination (Join-Path $session.evidence 'logs') -Recurse -Force
    }
    foreach ($folder in $session.shells, $session.local) {
        for ($attempt = 1; (Test-Path -LiteralPath $folder) -and $attempt -le 10; $attempt++) {
            Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
            if (Test-Path -LiteralPath $folder) { Start-Sleep -Milliseconds 500 }
        }
        if (Test-Path -LiteralPath $folder) { $problems.Add("$folder could not be removed") }
    }
    $session.shell = $null
    $session.folder = $null
    $session.pid = $null
    Save-Session
    $problems
}
