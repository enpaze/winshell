#requires -Version 7.0
[CmdletBinding()]
param(
    [string[]] $Id,
    [ValidateSet('command', 'service', 'widget', 'block')][string] $Kind,
    [switch] $List,
    [string] $Winshell,
    [switch] $Keep
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
$shells = [ordered]@{
    none = $null
    features = 'features/shell'
    starter = 'templates/winshell'
    'omarchy-style' = 'examples/omarchy-style'
}
$stepKeys = @('winshell', 'exit', 'output', 'file', 'within', 'background', 'for', 'element', 'window', 'has')
$commandTimeout = [TimeSpan]::FromMinutes(10)

function Read-FeatureMap {
    $Id = @($Id | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
    $map = @(Get-Content -LiteralPath (Join-Path $root 'features/map.json') -Raw | ConvertFrom-Json)
    $chosen = @($map | Where-Object {
            (-not $Id -or $Id -contains $_.id) -and (-not $Kind -or $_.kind -eq $Kind)
        })
    foreach ($wanted in $Id) {
        if (@($map | Where-Object id -EQ $wanted).Count -eq 0) { throw "features/map.json has no feature $wanted." }
    }
    $chosen
}

function Get-Field($Object, [string] $Name, $Default = $null) {
    if ($null -ne $Object -and $Object.PSObject.Properties.Name -contains $Name) { return $Object.$Name }
    $Default
}

function Expand-Text([string] $Text, [hashtable] $Values) {
    foreach ($key in $Values.Keys) { $Text = $Text.Replace("{$key}", [string] $Values[$key]) }
    $Text
}

function Start-Winshell([string[]] $Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($script:Exe)
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
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

function Invoke-Winshell([string[]] $Arguments, [TimeSpan] $Timeout = $commandTimeout) {
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
    @(Get-Instances | Where-Object { $_.configPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) }) |
        Select-Object -First 1
}

function Start-Shell([string] $Folder) {
    $result = Invoke-Winshell @('run', '-d', '-p', $Folder)
    if ($result.Exit -ne 0) { throw "winshell run -d -p $Folder exited with $($result.Exit): $($result.Text)" }
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($instance = Get-ShellInstance $Folder) {
            $tree = Invoke-Winshell @('inspect', '--pid', [string] $instance.pid, 'tree') ([TimeSpan]::FromSeconds(30))
            $windows = if ($tree.Exit -eq 0) { @($tree.Text | ConvertFrom-Json -Depth 1024) } else { @() }
            $empty = @($windows | Where-Object { -not (Get-Field (Get-Field $_ 'content') 'children') })
            if ($windows.Count -gt 0 -and $empty.Count -eq 0) { return $instance }
        }
        Start-Sleep -Milliseconds 250
    }
    throw "winshell run -d -p $Folder returned, but the shell never showed a window in winshell list -j and inspect tree."
}

function Get-ShellProcesses {
    @(Get-Process | Where-Object {
            $path = try { $_.Path } catch { $null }
            $path -and $path.StartsWith($script:Config, [StringComparison]::OrdinalIgnoreCase)
        })
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

function Copy-Shell([string] $Name, [string] $Source, [string] $Config) {
    $folder = Join-Path $Config $Name
    $paths = @(git -C $root -c core.quotepath=false ls-files --cached --others --exclude-standard -- $Source)
    if ($LASTEXITCODE -ne 0) { throw "Could not list the files of $Source with Git." }
    foreach ($path in $paths) {
        $from = Join-Path $root $path
        if (-not [IO.File]::Exists($from)) { continue }
        $to = Join-Path $folder $path.Substring($Source.Length + 1)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to)) | Out-Null
        [IO.File]::Copy($from, $to, $true)
    }
    $projectPath = @(Get-ChildItem -LiteralPath $folder -Filter '*.csproj')[0].FullName
    $project = [xml] (Get-Content -LiteralPath $projectPath -Raw)
    $sourceFolder = Join-Path $root $Source
    foreach ($node in @($project.SelectNodes('/Project/Import[@Project]'))) {
        $node.SetAttribute('Project', [IO.Path]::GetFullPath((Join-Path $sourceFolder $node.Project)))
    }
    foreach ($node in @($project.SelectNodes('/Project/ItemGroup/ProjectReference[@Include]'))) {
        $node.SetAttribute('Include', [IO.Path]::GetFullPath((Join-Path $sourceFolder $node.Include)))
    }
    $package = $project.SelectSingleNode('/Project/ItemGroup/PackageReference[@Include="Winshell"]')
    if ($null -ne $package) {
        $reference = $project.CreateElement('ProjectReference')
        $reference.SetAttribute('Include', (Join-Path $root 'src/Winshell.Shell/Winshell.Shell.csproj'))
        $package.ParentNode.ReplaceChild($reference, $package) | Out-Null
        $import = $project.CreateElement('Import')
        $import.SetAttribute('Project', (Join-Path $root 'src/Winshell.Package/buildTransitive/Winshell.props'))
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

function Find-Elements($Node, [string] $Name, [System.Collections.Generic.List[object]] $Found) {
    foreach ($key in 'name', 'automationId', 'label') {
        if ((Get-Field $Node $key) -ceq $Name) { $Found.Add($Node); return }
    }
    foreach ($child in @(Get-Field $Node 'children' @())) { Find-Elements $child $Name $Found }
}

function Get-Excerpt($Windows) {
    $lines = foreach ($window in $Windows) {
        "window $($window.window) $($window.kind) $(Get-Field (Get-Field $window 'content') 'type'):"
        $queue = [Collections.Generic.Queue[object]]::new()
        foreach ($root in @(Get-Field $window 'content') + @(Get-Field $window 'popups' @())) { if ($root) { $queue.Enqueue($root) } }
        while ($queue.Count -gt 0) {
            $node = $queue.Dequeue()
            $named = @('name', 'automationId', 'label', 'text') | Where-Object { Get-Field $node $_ } |
                ForEach-Object { "$_=$(Get-Field $node $_)" }
            if ($named) { "  $($node.type) $($named -join ' ')" }
            foreach ($child in @(Get-Field $node 'children' @())) { $queue.Enqueue($child) }
        }
    }
    (@($lines) | Select-Object -First 40) -join "`n"
}

function Test-Element($Step, [hashtable] $Values) {
    $tree = Invoke-Winshell @('inspect', '--pid', [string] $Values.pid, 'tree') ([TimeSpan]::FromSeconds(30))
    if ($tree.Exit -ne 0) { return [pscustomobject]@{ Passed = $false; Got = "inspect tree exited with $($tree.Exit): $($tree.Text)" } }
    $windows = @($tree.Text | ConvertFrom-Json -Depth 1024)
    $named = Get-Field $Step 'window'
    if ($null -ne $named) {
        $windows = @($windows | Where-Object {
                [string] $_.window -eq [string] $named -or $_.kind -eq $named -or (Get-Field (Get-Field $_ 'content') 'type') -eq $named
            } | Select-Object -First 1)
    }
    $found = [System.Collections.Generic.List[object]]::new()
    foreach ($window in $windows) {
        if ($content = Get-Field $window 'content') { Find-Elements $content $Step.element $found }
        foreach ($popup in @(Get-Field $window 'popups' @())) { Find-Elements $popup $Step.element $found }
    }
    if ($found.Count -ne 1) {
        return [pscustomobject]@{ Passed = $false; Got = "$($found.Count) elements named $($Step.element)`n$(Get-Excerpt $windows)" }
    }
    $element = $found[0]
    $has = Get-Field $Step 'has'
    $evidence = @()
    foreach ($property in @(if ($has) { $has.PSObject.Properties })) {
        $value = Get-Field $element $property.Name ''
        if ($value -is [bool]) { $value = $value.ToString().ToLowerInvariant() }
        $value = [string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0}', $value)
        if ($value -notmatch $property.Value) {
            return [pscustomobject]@{ Passed = $false; Got = "$($property.Name) is `"$value`"" }
        }
        $evidence += $value
    }
    [pscustomobject]@{ Passed = $true; Got = if ($evidence) { $evidence -join ' | ' } else { "$($Step.element) found" } }
}

function Test-Winshell($Step, [hashtable] $Values) {
    $arguments = @($Step.winshell | ForEach-Object { Expand-Text $_ $Values })
    $pattern = Get-Field $Step 'output'
    if ($null -ne $pattern) { $pattern = Expand-Text $pattern $Values }
    $for = Get-Field $Step 'for'
    if (Get-Field $Step 'background' $false) {
        $script:Background.Add((Start-Winshell $arguments))
        return [pscustomobject]@{ Passed = $true; Got = "started winshell $($arguments -join ' ')" }
    }
    if ($null -ne $for) {
        $started = Start-Winshell $arguments
        $null = $started.Process.WaitForExit([int] ([double] $for * 1000))
        Stop-Started $started
        $result = [pscustomobject]@{ Exit = $null; Text = Read-Started $started }
    }
    else {
        $result = Invoke-Winshell $arguments
    }
    $exits = @(Get-Field $Step 'exit' 0)
    if ($null -eq $for -and $exits -notcontains $result.Exit) {
        return [pscustomobject]@{ Passed = $false; Got = "exit $($result.Exit): $($result.Text)" }
    }
    $evidence = "exit $($result.Exit)"
    if ($null -ne $pattern) {
        $match = [regex]::Match($result.Text, $pattern, [Text.RegularExpressions.RegexOptions]::Multiline)
        if (-not $match.Success) { return [pscustomobject]@{ Passed = $false; Got = "output was: $($result.Text)" } }
        $evidence = $match.Value
    }
    $file = Get-Field $Step 'file'
    if ($null -ne $file) {
        $path = Expand-Text $file $Values
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
            return [pscustomobject]@{ Passed = $false; Got = "$path is missing or empty; output was: $($result.Text)" }
        }
        $evidence = "$path ($((Get-Item -LiteralPath $path).Length) bytes)"
    }
    [pscustomobject]@{ Passed = $true; Got = $evidence }
}

function Get-Expected($Step) {
    if ($null -ne (Get-Field $Step 'element')) {
        $has = @(if ($Step.PSObject.Properties.Name -contains 'has') { $Step.has.PSObject.Properties | ForEach-Object { "$($_.Name) ~ $($_.Value)" } })
        return "element $($Step.element)$(if ($has) { ' with ' + ($has -join ', ') })"
    }
    $parts = @("winshell $($Step.winshell -join ' ')")
    if ($null -ne (Get-Field $Step 'output')) { $parts += "output ~ $($Step.output)" }
    if ($null -ne (Get-Field $Step 'file')) { $parts += "file $($Step.file)" }
    if ($null -eq (Get-Field $Step 'for') -and -not (Get-Field $Step 'background' $false)) { $parts += "exit $(@(Get-Field $Step 'exit' 0) -join ' or ')" }
    $parts -join ', '
}

function Invoke-Feature($Feature, [string] $Folder, [string] $Scratch) {
    $values = @{ shell = $Folder; scratch = $Scratch; config = $script:Config; local = $script:Local; pid = '' }
    $n = 0
    $last = ''
    foreach ($step in $Feature.steps) {
        $n++
        $unknown = @($step.PSObject.Properties.Name | Where-Object { $stepKeys -notcontains $_ })
        if ($unknown) { return "FAIL $($Feature.id)  step ${n}: keys from the step vocabulary / $($unknown -join ', ')" }
        $isElement = $null -ne (Get-Field $step 'element')
        $usesPid = $isElement -or ($step | ConvertTo-Json -Depth 8 -Compress).Contains('{pid}')
        if ($usesPid -and $Folder) {
            $instance = Get-ShellInstance $Folder
            if (-not $instance) { $instance = Start-Shell $Folder }
            $values.pid = [string] $instance.pid
        }
        $within = [double] (Get-Field $step 'within' $(if ($isElement) { 10 } else { 0 }))
        $deadline = [DateTime]::UtcNow.AddSeconds($within)
        while ($true) {
            $result = if ($isElement) { Test-Element $step $values } else { Test-Winshell $step $values }
            if ($result.Passed -or [DateTime]::UtcNow -ge $deadline) { break }
            Start-Sleep -Milliseconds 500
            if ($usesPid -and $Folder -and ($instance = Get-ShellInstance $Folder)) { $values.pid = [string] $instance.pid }
        }
        if (-not $result.Passed) { return "FAIL $($Feature.id)  step ${n}: $(Get-Expected $step) / $($result.Got)" }
        $last = $result.Got
    }
    "PASS $($Feature.id)  $(($last -split "`r?`n")[0])"
}

$features = @(Read-FeatureMap)
if ($List) {
    $features | ForEach-Object {
        [pscustomobject]@{ Id = $_.id; Kind = $_.kind; Shell = $_.shell; Code = ($_.code -join ', ') }
    } | Format-Table -AutoSize | Out-String -Width 400 | Write-Output
    exit 0
}

$temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$session = Join-Path $temporary ('winshell-features-' + [guid]::NewGuid().ToString('N'))
$script:Config = Join-Path $session 'config'
$script:Local = Join-Path $session 'local'
$script:Background = [System.Collections.Generic.List[object]]::new()
$saved = @{ WINSHELL_CONFIG_DIR = $env:WINSHELL_CONFIG_DIR; WINSHELL_LOCAL_DIR = $env:WINSHELL_LOCAL_DIR }
$began = [DateTime]::Now
$failed = 0
try {
    [IO.Directory]::CreateDirectory($script:Config) | Out-Null
    [IO.Directory]::CreateDirectory($script:Local) | Out-Null
    $env:WINSHELL_CONFIG_DIR = $script:Config
    $env:WINSHELL_LOCAL_DIR = $script:Local
    if ($Winshell) {
        $script:Exe = (Resolve-Path -LiteralPath $Winshell).ProviderPath
    }
    else {
        dotnet build (Join-Path $root 'src/Winshell/Winshell.csproj') -c Release --nologo -v q | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'src/Winshell did not build.' }
        $script:Exe = @(Get-ChildItem -LiteralPath (Join-Path $root 'src/Winshell/bin/Release') -Recurse -Filter 'winshell.exe')[0].FullName
    }
    foreach ($name in '.editorconfig', 'global.json', 'Directory.Build.props', 'Directory.Build.targets') {
        Copy-Item -LiteralPath (Join-Path $root $name) -Destination (Join-Path $script:Config $name)
    }
    foreach ($shell in $shells.Keys) {
        $group = @($features | Where-Object shell -EQ $shell)
        if ($group.Count -eq 0) { continue }
        $folder = ''
        $broken = $null
        if ($shells[$shell]) {
            try {
                $folder = Copy-Shell $shell $shells[$shell] $script:Config
                $instance = Start-Shell $folder
                Write-Output "shell $shell started, pid $($instance.pid)"
            }
            catch {
                $broken = "shell $shell did not start: $($_.Exception.Message)"
            }
        }
        foreach ($feature in $group) {
            if ($broken) {
                Write-Output "FAIL $($feature.id)  $broken"
                $failed++
                continue
            }
            $scratch = Join-Path $session "scratch/$($feature.id)"
            [IO.Directory]::CreateDirectory($scratch) | Out-Null
            try {
                $line = Invoke-Feature $feature $folder $scratch
            }
            catch {
                $line = "FAIL $($feature.id)  $($_.Exception.Message)"
            }
            finally {
                if ($script:Background.Count -gt 0) {
                    foreach ($started in $script:Background) { Stop-Started $started }
                    $script:Background.Clear()
                    Stop-Shells
                }
            }
            if ($line.StartsWith('FAIL')) { $failed++ }
            Write-Output $line
        }
        if ($folder) { Stop-Shells }
    }
}
finally {
    foreach ($started in $script:Background) { Stop-Started $started }
    if ($script:Exe) {
        Stop-Shells
        $left = @(Get-Instances)
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        do {
            $stray = @(Get-Process | Where-Object {
                    $path = try { $_.Path } catch { $null }
                    $path -and ($path.StartsWith($session, [StringComparison]::OrdinalIgnoreCase) -or
                        ($path -eq $script:Exe -and $_.StartTime -ge $began -and $_.Id -ne $PID))
                })
            if ($stray.Count -eq 0) { break }
            Start-Sleep -Milliseconds 500
        } while ([DateTime]::UtcNow -lt $deadline)
        foreach ($process in $stray) {
            Write-Output "FAIL teardown  $($process.ProcessName) $($process.Id) was still running, so it was ended"
            $process.Kill($true)
            $failed++
        }
        foreach ($instance in $left) {
            Write-Output "FAIL teardown  instance $($instance.id) was still listed"
            $failed++
        }
    }
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
    if ($Keep) {
        Write-Output "Kept $session"
    }
    elseif (Test-Path -LiteralPath $session) {
        Remove-Item -LiteralPath $session -Recurse -Force -ErrorAction SilentlyContinue
    }
}
exit ([int] ($failed -gt 0))
