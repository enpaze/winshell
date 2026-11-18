#requires -Version 7.0
[CmdletBinding()]
param(
    [string[]] $Id,
    [ValidateSet('command', 'service', 'widget', 'block')][string] $Kind,
    [switch] $List,
    [switch] $Check,
    [string] $Winshell,
    [string] $Session,
    [string] $Map = (Join-Path (Split-Path -Parent $PSScriptRoot) 'features')
)

. (Join-Path $PSScriptRoot 'Session.ps1')
. (Join-Path $PSScriptRoot 'FeatureMap.ps1')

function Write-Line([string] $Line) {
    Write-Output $Line
    if ($script:Run) { Add-Content -LiteralPath (Join-Path $script:Run.evidence 'output.txt') -Value $Line -Encoding utf8NoBOM }
}

function Expand-Text([string] $Text, [hashtable] $Values) {
    foreach ($key in $Values.Keys) { $Text = $Text.Replace("{$key}", [string] $Values[$key]) }
    $Text
}

function Write-Transcript([string] $Scratch, [string] $Text) {
    Add-Content -LiteralPath (Join-Path $Scratch 'steps.txt') -Value $Text -Encoding utf8NoBOM
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

function Test-Element($Step, [hashtable] $Values, [string] $Evidence) {
    $tree = Get-Tree ([int] $Values.pid)
    Set-Content -LiteralPath $Evidence -Value $tree.Text -Encoding utf8NoBOM
    if ($tree.Exit -ne 0) { return [pscustomobject]@{ Passed = $false; Got = "inspect tree exited with $($tree.Exit): $($tree.Text)" } }
    $windows = $tree.Windows
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

function Test-Winshell($Step, [hashtable] $Values, [string] $Scratch) {
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
    Write-Transcript $Scratch "$ winshell $($arguments -join ' ')`nexit $($result.Exit)`n$($result.Text)`n"
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
    $parts = @(Get-StepCommand $Step)
    if ($null -ne (Get-Field $Step 'element')) {
        $has = @(if ($Step.PSObject.Properties.Name -contains 'has') { $Step.has.PSObject.Properties | ForEach-Object { "$($_.Name) ~ $($_.Value)" } })
        return "$parts finds $($Step.element)$(if ($has) { ' with ' + ($has -join ', ') })"
    }
    if ($null -ne (Get-Field $Step 'output')) { $parts += "output ~ $($Step.output)" }
    if ($null -ne (Get-Field $Step 'file')) { $parts += "file $($Step.file)" }
    if ($null -eq (Get-Field $Step 'for') -and -not (Get-Field $Step 'background' $false)) { $parts += "exit $(@(Get-Field $Step 'exit' 0) -join ' or ')" }
    $parts -join ', '
}

function Invoke-Feature($Feature, [string] $Folder, [string] $Scratch) {
    $values = @{ shell = $Folder; scratch = $Scratch; pid = '' }
    $n = 0
    $last = ''
    foreach ($step in $Feature.steps) {
        $n++
        $isElement = $null -ne (Get-Field $step 'element')
        $usesPid = $isElement -or ($step | ConvertTo-Json -Depth 8 -Compress).Contains('{pid}')
        if ($usesPid -and $Folder) {
            $instance = Wait-ShellInstance $Folder 30
            if (-not $instance) { $instance = Start-ShellFolder $Folder }
            $values.pid = [string] $instance.pid
        }
        $within = [double] (Get-Field $step 'within' $(if ($isElement) { 10 } else { 0 }))
        $deadline = [DateTime]::UtcNow.AddSeconds($within)
        while ($true) {
            $result = if ($isElement) { Test-Element $step $values (Join-Path $Scratch "tree-$n.json") } else { Test-Winshell $step $values $Scratch }
            if ($result.Passed -or [DateTime]::UtcNow -ge $deadline) { break }
            Start-Sleep -Milliseconds 500
            if ($usesPid -and $Folder -and ($instance = Get-ShellInstance $Folder)) { $values.pid = [string] $instance.pid }
        }
        Write-Transcript $Scratch "step ${n}: $(if ($result.Passed) { 'PASS' } else { 'FAIL' }) $(Get-Expected $step)`n  got: $($result.Got)`n"
        if (-not $result.Passed) { return "FAIL $($Feature.id)  step ${n}: $(Get-Expected $step) / $($result.Got)" }
        $last = $result.Got
    }
    "PASS $($Feature.id)  $(($last -split "`r?`n")[0])"
}

$all = @(Read-FeatureMap $Map)
$problems = @(foreach ($feature in $all) { foreach ($problem in $feature.problems) { "$($feature.id).md: $problem" } }) +
    @(Get-IndexProblems $Map $all)
if ($problems.Count -gt 0) {
    $problems | ForEach-Object { Write-Output "MAP $_" }
    Write-Output "The feature map in $Map has $($problems.Count) problems. Fix each file to the shape features/README.md describes."
    exit 1
}
if ($Check) {
    Write-Output "The feature map in $Map is well formed: $($all.Count) features."
    exit 0
}

$Id = @($Id | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
foreach ($wanted in $Id) {
    if (@($all | Where-Object id -EQ $wanted).Count -eq 0) { throw "The feature map has no feature $wanted. drive.ps1 -List lists them." }
}
$features = @($all | Where-Object { (-not $Id -or $Id -contains $_.id) -and (-not $Kind -or $_.kind -eq $Kind) })
if ($List) {
    $features | ForEach-Object { [pscustomobject]@{ Id = $_.id; Shell = $_.shell; Steps = $_.steps.Count; Title = $_.title } } |
        Format-Table -AutoSize | Out-String -Width 400 | Write-Output
    exit 0
}

Assert-NoOtherRun
$script:Background = [System.Collections.Generic.List[object]]::new()
$failed = 0
$null = New-Session $Session $Winshell
try {
    Write-Line "session $($script:Run.path), winshell $($script:Run.exe)"
    foreach ($shell in $script:Shells.Keys) {
        $group = @($features | Where-Object shell -EQ $shell)
        if ($group.Count -eq 0) { continue }
        $folder = ''
        $broken = $null
        if ($script:Shells[$shell]) {
            try {
                $instance = Start-Shell $shell
                $folder = $script:Run.folder
                Write-Line "shell $shell started, pid $($instance.pid)"
            }
            catch {
                $broken = "shell $shell did not start: $($_.Exception.Message)"
            }
        }
        foreach ($feature in $group) {
            if ($broken) {
                Write-Line "FAIL $($feature.id)  $broken"
                $failed++
                continue
            }
            $scratch = Join-Path $script:Run.evidence $feature.id
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
            Write-Line $line
        }
        if ($folder) { Stop-Shells }
    }
}
finally {
    foreach ($started in $script:Background) { Stop-Started $started }
    $passed = $features.Count - $failed
    foreach ($problem in Stop-Session) {
        Write-Line "FAIL teardown  $problem"
        $failed++
    }
    Write-Line "$passed of $($features.Count) features passed. Evidence: $($script:Run.evidence)"
}
exit ([int] ($failed -gt 0))
