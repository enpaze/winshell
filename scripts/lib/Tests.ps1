$script:Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$script:TrxNamespace = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'

function Get-TestRun {
    param([ValidateSet('Tests', 'Docs', 'Package')][string] $Tier = 'Tests', [ValidateRange(0, 4)][int] $Shard = 0)
    $run = switch ($Tier) {
        'Docs' { @{ Project = 'tests\Winshell.Shell.Tests'; Filter = 'FullyQualifiedName~Winshell.ShellTests.DocsTests' } }
        'Package' { @{ Project = 'tests\Winshell.Package.Tests'; Filter = 'TestCategory=Package' } }
        default { @{ Project = 'winshell.slnx'; Filter = 'TestCategory!=Package' } }
    }
    $run.Projects = @($run.Project)
    $run.CompileTemplateExamplesReferenceAndLogo = $false
    if ($run.Project -like '*.slnx') {
        $solution = [xml] (Get-Content -LiteralPath (Join-Path $script:Root $run.Project) -Raw)
        $projects = @($solution.SelectNodes('//Project') | Where-Object { $_.Path -like '*.Tests.csproj' } | ForEach-Object Path)
        if ($Shard -gt 0) {
            $run.Projects = @($projects | Where-Object {
                $owner = switch ([IO.Path]::GetFileNameWithoutExtension($_)) {
                    'Winshell.Tests' { 1 }
                    'Winshell.Shell.Tests' { 2 }
                    'Winshell.Services.Tests' { 3 }
                    default { 4 }
                }
                $owner -eq $Shard
            })
            if ($run.Projects.Count -eq 0) { throw "Test shard $Shard has no projects." }
            $run.CompileTemplateExamplesReferenceAndLogo = @($run.Projects | Where-Object {
                [IO.Path]::GetFileNameWithoutExtension($_) -eq 'Winshell.Shell.Tests'
            }).Count -gt 0
        }
        else {
            $run.Projects = $projects
            $run.Filter += '&TestCategory!=Window'
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
        [Parameter(Mandatory)][string] $ResultsDirectory
    )
    dotnet test --project (Join-Path $script:Root $Project) --no-build --filter $Filter --report-trx --results-directory $ResultsDirectory | Out-Host
    $LASTEXITCODE
}

function Invoke-TestRun {
    param(
        [ValidateSet('Tests', 'Docs', 'Package')][string] $Tier = 'Tests',
        [ValidateRange(0, 4)][int] $Shard = 0,
        [string] $ResultsDirectory
    )
    $parent = if ($ResultsDirectory) { $ResultsDirectory } else { [IO.Path]::GetTempPath() }
    $results = Join-Path $parent ('winshell-test-results-' + [guid]::NewGuid().ToString('N'))
    try {
        $run = Get-TestRun -Tier $Tier -Shard $Shard
        $watch = $Tier -eq 'Tests' -and $Shard -eq 0
        if ($watch -and -not ('WinshellWindowWatch' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'WindowWatch.cs') }
        $exit = 0
        $windows = @()
        foreach ($project in $run.Projects) {
            $watcher = if ($watch) { [WinshellWindowWatch]::new($PID) }
            $code = Invoke-DotnetTest -Project $project -Filter $run.Filter -ResultsDirectory $results
            if ($code -ne 0) { $exit = $code }
            if ($watcher) {
                $name = [IO.Path]::GetFileNameWithoutExtension($project)
                $windows += @($watcher.Stop() | ForEach-Object { "$name showed a window: $_. Give the test that shows it the category Window: [TestCategory(`"Window`")]." })
            }
        }
        $outcome = try { Read-TestResult -Directory $results -ProjectCount $run.Projects.Count } catch { if ($exit -eq 0) { throw } }
        if ($outcome) { foreach ($skip in $outcome.Skipped) { Write-Host "Skipped $skip" } }
        if ($outcome) { foreach ($test in $outcome.Slowest) { Write-Host "Slow test $($test.Duration): $($test.Name)" } }
        foreach ($window in $windows) { Write-Host $window }
        if ($exit -ne 0) { return $exit }
        if ($windows) { return 1 }
        if ($outcome.Passed -gt 0 -and $outcome.Failed -eq 0 -and $outcome.Complete) { return 0 }
        Write-Host 'A test failed, no test ran, or a test project wrote no results.'
        return 1
    }
    finally {
        if (-not $ResultsDirectory) { Remove-Item -LiteralPath $results -Recurse -Force -ErrorAction SilentlyContinue }
    }
}
