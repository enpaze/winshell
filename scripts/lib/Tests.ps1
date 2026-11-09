# Private test runner for check.ps1. Exit codes alone do not prove every project ran tests.
$script:Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$script:TrxNamespace = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'

# What a run builds, the test projects it runs one after another, and a filter. Docs builds the guide samples and checks
# the generated reference; Package packs the template and runs a shell from it. Each test project writes one results
# file, so ProjectCount is how many to expect.
function Get-TestRun {
    param([ValidateSet('Tests', 'Docs', 'Package')][string] $Tier = 'Tests', [ValidateRange(0, 5)][int] $Shard = 0)
    $run = switch ($Tier) {
        'Docs' { @{ Project = 'tests\Winshell.Shell.Tests'; Filter = 'FullyQualifiedName~Winshell.ShellTests.DocsTests' } }
        'Package' { @{ Project = 'tests\Winshell.Package.Tests'; Filter = 'Category=Package' } }
        default { @{ Project = 'winshell.slnx'; Filter = 'Category!=Package' } }
    }
    $run.Projects = @($run.Project)
    $run.ProjectCount = 1
    if ($run.Project -like '*.slnx') {
        $solution = [xml] (Get-Content -LiteralPath (Join-Path $script:Root $run.Project) -Raw)
        $projects = @($solution.SelectNodes('//Project') | Where-Object { $_.Path -like '*.Tests.csproj' } | ForEach-Object Path)
        if ($Shard -gt 0) {
            # Keep each desktop/process owner on its own CI desktop; all other projects share the fourth worker.
            # A new test project automatically joins that worker instead of silently disappearing from CI.
            $run.Projects = @($projects | Where-Object {
                $owner = switch ([IO.Path]::GetFileNameWithoutExtension($_)) {
                    'Winshell.Tests' { if ($Shard -eq 5) { 5 } else { 1 } }
                    'Winshell.Shell.Tests' { 2 }
                    'Winshell.Services.Tests' { 3 }
                    default { 4 }
                }
                $owner -eq $Shard
            })
            if ($run.Projects.Count -eq 0) { throw "Test shard $Shard has no projects." }
            $run.ProjectCount = $run.Projects.Count
            # These are complementary filters: a new CLI class stays in the first worker. Widget tests run many
            # real git/build processes; keep them off the desktop that dev and reload use to compile their shells.
            $widgets = 'FullyQualifiedName~Winshell.Tests.WidgetCommandTests.'
            if ($Shard -eq 1) { $run.Filter += '&' + $widgets.Replace('~', '!~') }
            if ($Shard -eq 5) { $run.Filter += '&' + $widgets }
        }
        else {
            # The local check opens no windows, so any number of them can run at once. The CLI and Shell suites, and
            # a test elsewhere in the category Window, run only in CI's shards.
            $run.Projects = @($projects | Where-Object { [IO.Path]::GetFileNameWithoutExtension($_) -notin 'Winshell.Tests', 'Winshell.Shell.Tests' })
            $run.ProjectCount = $run.Projects.Count
            $run.Filter += '&Category!=Window'
        }
    }
    $run
}

function Read-TestResult {
    param(
        [Parameter(Mandatory)][string] $Directory,
        [Parameter(Mandatory)][int] $ProjectCount
    )
    $files = @(Get-ChildItem -LiteralPath $Directory -Filter '*.trx' -ErrorAction SilentlyContinue)
    $passed = 0
    $failed = 0
    $skipped = @()
    $timings = @()
    $complete = $files.Count -eq $ProjectCount
    foreach ($file in $files) {
        $xml = [xml]::new()
        $xml.Load($file.FullName)
        $names = [Xml.XmlNamespaceManager]::new($xml.NameTable)
        $names.AddNamespace('t', $script:TrxNamespace)
        if ($xml.SelectNodes('//t:RunInfo', $names) | Where-Object { $_.InnerText -match 'test run was aborted' }) { $complete = $false }
        $summary = $xml.SelectSingleNode('//t:ResultSummary', $names).GetAttribute('outcome')
        $failures = [int] $xml.SelectSingleNode('//t:Counters', $names).GetAttribute('failed')
        if ($summary -ne 'Completed' -and $failures -eq 0) { $complete = $false }
        $passed += $xml.SelectNodes('//t:UnitTestResult[@outcome="Passed"]', $names).Count
        $failed += $failures
        foreach ($test in $xml.SelectNodes('//t:UnitTestResult[@duration]', $names)) {
            $timings += [pscustomobject]@{ Name = $test.GetAttribute('testName'); Duration = [timespan]::Parse($test.GetAttribute('duration')) }
        }
        # VSTest's console shows a skipped test without its reason, which only the results file keeps.
        foreach ($skip in $xml.SelectNodes('//t:UnitTestResult[@outcome="NotExecuted"]', $names)) {
            $reason = $skip.SelectSingleNode('t:Output/t:ErrorInfo/t:Message', $names)
            $skipped += "$($skip.GetAttribute('testName')): $(if ($reason) { $reason.InnerText } else { 'no reason given' })"
        }
    }
    [pscustomobject]@{
        Passed = $passed; Failed = $failed; Skipped = $skipped; Complete = $complete
        Slowest = @($timings | Sort-Object Duration -Descending | Select-Object -First 10)
    }
}

function Invoke-DotnetTest {
    param(
        [Parameter(Mandatory)][string] $Project,
        [Parameter(Mandatory)][string] $Filter,
        [Parameter(Mandatory)][string] $ResultsDirectory,
        [int] $Seed = 1
    )
    # A native hang timeout ends the run and names the test. More threads than cores expose contention.
    dotnet test (Join-Path $script:Root $Project) --no-build --nologo --filter $Filter --logger trx --results-directory $ResultsDirectory --blame-hang-timeout 3m --blame-hang-dump-type none -- "xUnit.Seed=$Seed" xUnit.MaxParallelThreads=2x | Out-Host
    $LASTEXITCODE
}

function Invoke-TestRun {
    param(
        [ValidateSet('Tests', 'Docs', 'Package')][string] $Tier = 'Tests',
        [int] $Seed = 0,
        [ValidateRange(0, 5)][int] $Shard = 0,
        [string] $ResultsDirectory
    )
    if ($Seed -eq 0) { $Seed = Get-Random -Minimum 1 -Maximum ([int]::MaxValue) }
    $replay = if ($Shard -gt 0) { "end a dotnet test command with -- xUnit.Seed=$Seed" } else { "pwsh scripts/check.ps1 -Tier $Tier -Seed $Seed" }
    Write-Host "Test order seed: $Seed. To run this order again: $replay"
    # A caller-supplied parent retains even a crashed run's evidence. A unique child prevents stale results from
    # satisfying the missing-project guard when the parent was used before. Local runs still clean up automatically.
    $parent = if ($ResultsDirectory) { $ResultsDirectory } else { [IO.Path]::GetTempPath() }
    $results = Join-Path $parent ('winshell-test-results-' + [guid]::NewGuid().ToString('N'))
    try {
        $run = Get-TestRun -Tier $Tier -Shard $Shard
        $exit = 0
        foreach ($project in $run.Projects) {
            $code = Invoke-DotnetTest -Project $project -Filter $run.Filter -ResultsDirectory $results -Seed $Seed
            if ($code -ne 0) { $exit = $code }
        }
        # A crashed run can leave a results file that doesn't read; its exit code still decides.
        $outcome = try { Read-TestResult -Directory $results -ProjectCount $run.ProjectCount } catch { if ($exit -eq 0) { throw } }
        if ($outcome) { foreach ($skip in $outcome.Skipped) { Write-Host "Skipped $skip" } }
        if ($outcome) { foreach ($test in $outcome.Slowest) { Write-Host "Slow test $($test.Duration): $($test.Name)" } }
        if ($exit -ne 0) { return $exit }
        if ($outcome.Passed -gt 0 -and $outcome.Failed -eq 0 -and $outcome.Complete) { return 0 }
        Write-Host 'A test failed, no test ran, or a test project wrote no results.'
        return 1
    }
    finally {
        if (-not $ResultsDirectory) { Remove-Item -LiteralPath $results -Recurse -Force -ErrorAction SilentlyContinue }
    }
}
