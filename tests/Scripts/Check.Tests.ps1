BeforeAll {
    . (Join-Path $PSScriptRoot '../../scripts/lib/Tests.ps1')

    # Writes a results file like dotnet test does. Rows are "Class.Method=Outcome", or "Class.Method=NotExecuted:reason".
    function Write-Trx {
        param([string] $Directory, [string[]] $Rows, [switch] $Crashed, [switch] $NoAbortText)
        New-Item -ItemType Directory -Force -Path $Directory | Out-Null
        $definitions = ''
        $results = ''
        foreach ($row in $Rows) {
            $name, $outcome = $row -split '=', 2
            $outcome, $reason = $outcome -split ':', 2
            $id = [guid]::NewGuid()
            $class = $name.Substring(0, $name.LastIndexOf('.'))
            $method = $name.Substring($name.LastIndexOf('.') + 1)
            $definitions += "<UnitTest name=`"$name(a: 1)`" id=`"$id`"><TestMethod className=`"$class`" name=`"$method`" /></UnitTest>"
            $output = if ($reason) { "<Output><ErrorInfo><Message>$reason</Message></ErrorInfo></Output>" } else { '' }
            $results += "<UnitTestResult testId=`"$id`" testName=`"$name`" outcome=`"$outcome`">$output</UnitTestResult>"
        }
        $failures = @($Rows | Where-Object { $_ -like '*=Failed' }).Count
        $outcome = if ($Crashed -or $NoAbortText -or $failures -gt 0) { 'Failed' } else { 'Completed' }
        $info = if ($Crashed) {
            '<RunInfo outcome="Error"><Text>The active test run was aborted. Reason: Test host process crashed</Text></RunInfo>'
        }
        elseif ($failures -gt 0) {
            '<RunInfo outcome="Error"><Text>[xUnit.net 00:00:00.21] Ns.Class.Test [FAIL]</Text></RunInfo>'
        }
        else {
            '<RunInfo outcome="Warning"><Text>No test matches</Text></RunInfo>'
        }
        $xml = "<TestRun xmlns=`"$script:TrxNamespace`"><Results>$results</Results><TestDefinitions>$definitions</TestDefinitions><ResultSummary outcome=`"$outcome`"><Counters failed=`"$failures`" /><RunInfos>$info</RunInfos></ResultSummary></TestRun>"
        Set-Content -LiteralPath (Join-Path $Directory ([guid]::NewGuid().ToString('N') + '.trx')) -Value $xml
    }
}

Describe 'Choosing tests' {
    BeforeAll {
        # A solution with the CLI, Shell, Services and Core projects, and a project added later.
        function Set-WinshellSolution {
            $script:Projects = @('Winshell.Tests', 'Winshell.Shell.Tests', 'Winshell.Services.Tests', 'Winshell.Core.Tests', 'New.Tests') |
                ForEach-Object { "tests/$_/$_.csproj" }
            $nodes = ($script:Projects | ForEach-Object { "<Project Path=`"$_`" />" }) -join ''
            Set-Content -LiteralPath (Join-Path $TestDrive 'winshell.slnx') -Value "<Solution>$nodes</Solution>"
        }
    }

    It 'runs locally only the projects and tests that open no windows, and no package tests' {
        $root = $script:Root
        $script:Root = $TestDrive
        try {
            Set-WinshellSolution
            $run = Get-TestRun
            $run.Projects | Should -Be $script:Projects
            $run.ProjectCount | Should -Be 5
            $run.Filter | Should -Be 'Category!=Package&Category!=Window'
        } finally { $script:Root = $root }
    }

    It 'partitions every solution test project once, includes new projects in the fourth shard, and runs windowed tests' {
        $root = $script:Root
        $script:Root = $TestDrive
        try {
            Set-WinshellSolution
            $routed = @(foreach ($shard in 1..4) { (Get-TestRun -Shard $shard).Projects })
            ($routed | Sort-Object) | Should -Be ($script:Projects | Sort-Object)
            (Get-TestRun -Shard 4).ProjectCount | Should -Be 2
            (Get-TestRun -Shard 5).Projects | Should -Be @('tests/Winshell.Tests/Winshell.Tests.csproj')
            foreach ($shard in 1..5) { (Get-TestRun -Shard $shard).Filter | Should -Not -Match 'Category!=Window' }
        } finally { $script:Root = $root }
    }
}

Describe 'Reading test results' {
    It 'counts passed and failed theory rows' {
        $directory = Join-Path $TestDrive 'theory'
        Write-Trx -Directory $directory -Rows @('Ns.Class.Theory=Passed', 'Ns.Class.Theory=Failed', 'Ns.Class.Fact=Passed')
        $result = Read-TestResult -Directory $directory -ProjectCount 1
        $result.Failed | Should -Be 1
        $result.Passed | Should -Be 2
        $result.Complete | Should -BeTrue
    }

    It 'names each skipped test with its reason' {
        $directory = Join-Path $TestDrive 'skipped'
        Write-Trx -Directory $directory -Rows @('Ns.Class.Fact=Passed', 'Ns.Class.Busy=NotExecuted:Someone was using this PC', 'Ns.Class.Bare=NotExecuted')
        (Read-TestResult -Directory $directory -ProjectCount 1).Skipped |
            Should -Be @('Ns.Class.Busy: Someone was using this PC', 'Ns.Class.Bare: no reason given')
    }

    It 'is not complete when a test host crashed' {
        $directory = Join-Path $TestDrive 'crash'
        Write-Trx -Directory $directory -Rows @('Ns.Class.Fact=Failed') -Crashed
        (Read-TestResult -Directory $directory -ProjectCount 1).Complete | Should -BeFalse
    }

    It 'is complete after an ordinary failure' {
        $directory = Join-Path $TestDrive 'failure'
        Write-Trx -Directory $directory -Rows @('Ns.Class.Fact=Failed')
        (Read-TestResult -Directory $directory -ProjectCount 1).Complete | Should -BeTrue
    }

    It 'is not complete when a run failed with no failed test' {
        $directory = Join-Path $TestDrive 'silent'
        Write-Trx -Directory $directory -Rows @('Ns.Class.Fact=Passed') -NoAbortText
        (Read-TestResult -Directory $directory -ProjectCount 1).Complete | Should -BeFalse
    }

    It 'is not complete when a test project wrote no results' {
        $directory = Join-Path $TestDrive 'missing'
        Write-Trx -Directory $directory -Rows @('Ns.Class.Fact=Failed')
        (Read-TestResult -Directory $directory -ProjectCount 2).Complete | Should -BeFalse
    }
}

Describe 'Running the suite' {
    BeforeAll {
        # A solution of this many test projects, which each write one results file.
        function Set-Solution([int] $Projects) {
            $paths = (1..$Projects | ForEach-Object { "<Project Path=`"tests/T$_.Tests/T$_.Tests.csproj`" />" }) -join ''
            Set-Content -LiteralPath (Join-Path $TestDrive 'winshell.slnx') -Value "<Solution>$paths</Solution>"
        }
        $script:RealRoot = $script:Root
        $script:Root = $TestDrive
    }

    AfterAll { $script:Root = $script:RealRoot }

    BeforeEach {
        Set-Solution 1
        $script:Exit = 0
        $script:Rows = @('Ns.Class.Good=Passed')
        # A project that writes no results file, as after a test host crash.
        $script:Silent = $null
        Mock Invoke-DotnetTest {
            $script:WrittenResults = $ResultsDirectory
            if ($Project -ne $script:Silent) { Write-Trx -Directory $ResultsDirectory -Rows $script:Rows }
            $script:Exit
        }
    }

    It 'passes when every test passes' {
        Invoke-TestRun -Seed 7 | Should -Be 0
        Should -Invoke Invoke-DotnetTest -Times 1 -Exactly -ParameterFilter { $Seed -eq 7 }
        Test-Path -LiteralPath $script:WrittenResults | Should -BeFalse
    }

    It 'retains evidence from a <Outcome> run in a caller-owned folder' -ForEach @(
        @{ Outcome = 'passing'; Exit = 0; Row = 'Ns.Class.Good=Passed' }
        @{ Outcome = 'failing'; Exit = 1; Row = 'Ns.Class.Bad=Failed' }
    ) {
        $script:Rows = @($Row)
        $script:Exit = $Exit
        $parent = Join-Path $TestDrive "retained-$Outcome"
        Invoke-TestRun -Seed 7 -ResultsDirectory $parent | Should -Be $Exit
        @(Get-ChildItem -LiteralPath $parent -Filter '*.trx' -Recurse) | Should -HaveCount 1
        # Reusing that folder must not let the previous complete run hide a missing test project.
        Set-Solution 2
        $script:Silent = 'tests/T2.Tests/T2.Tests.csproj'
        $script:Rows = @('Ns.Class.Good=Passed')
        $script:Exit = 0
        Invoke-TestRun -Seed 7 -ResultsDirectory $parent | Should -Be 1
        @(Get-ChildItem -LiteralPath $parent -Directory) | Should -HaveCount 2
    }

    It 'fails on a failed test, once, with nothing run again' {
        $script:Rows = @('Ns.Class.Good=Passed', 'Ns.Class.Bad=Failed')
        $script:Exit = 1
        Invoke-TestRun | Should -Be 1
        Should -Invoke Invoke-DotnetTest -Times 1 -Exactly
    }

    It 'fails a run that exits 0 having run no tests' {
        $script:Rows = @()
        Invoke-TestRun | Should -Be 1
    }

    It 'fails when a results file reports failure despite exit code 0' {
        $script:Rows = @('Ns.Class.Good=Passed', 'Ns.Class.Bad=Failed')
        Invoke-TestRun | Should -Be 1
    }

    It 'fails a run that exits 0 with a test project missing' {
        Set-Solution 2
        $script:Silent = 'tests/T2.Tests/T2.Tests.csproj'
        Invoke-TestRun | Should -Be 1
    }

    It 'names each skipped test with its reason when the run <Run>' -ForEach @(
        @{ Run = 'passes'; Exit = 0; Other = 'Ns.Class.Good=Passed' }
        @{ Run = 'fails'; Exit = 1; Other = 'Ns.Class.Bad=Failed' }
    ) {
        Mock Write-Host { }
        $script:Rows = @($Other, 'Ns.Class.Busy=NotExecuted:Someone was using this PC')
        $script:Exit = $Exit
        Invoke-TestRun | Should -Be $Exit
        Should -Invoke Write-Host -ParameterFilter { $Object -eq 'Skipped Ns.Class.Busy: Someone was using this PC' }
    }

    It 'chooses a seed of its own and prints it' {
        Mock Write-Host { }
        Invoke-TestRun | Should -Be 0
        Should -Invoke Write-Host -ParameterFilter { $Object -match 'Test order seed: \d+' }
    }

    It 'prints how to play back the tier it ran' {
        Mock Write-Host { }
        Invoke-TestRun -Seed 7 | Should -Be 0
        Should -Invoke Write-Host -ParameterFilter { $Object -match 'check\.ps1 -Tier Tests -Seed 7' }
    }

    # A shard runs windowed tests, which check.ps1 leaves out.
    It 'prints how to play back a shard with dotnet test' {
        Mock Write-Host { }
        Invoke-TestRun -Seed 7 -Shard 4 | Should -Be 0
        Should -Invoke Write-Host -ParameterFilter { $Object -match '-- xUnit\.Seed=7' }
        Should -Invoke Write-Host -Times 0 -ParameterFilter { $Object -match 'check\.ps1' }
    }
}

Describe 'Check command' {
    BeforeAll {
        $script:Check = (Resolve-Path (Join-Path $PSScriptRoot '../../scripts/check.ps1')).Path
        $script:ProjectCount = (Get-TestRun).ProjectCount

        # pwsh prepends the user module folder to an inherited PSModulePath at startup, where a developer's real Pester
        # lives. Setting the path inside the child, after startup, leaves only the stub visible.
        function Invoke-Check {
            param([hashtable] $Arguments = @{})
            pwsh -NoProfile -Command {
                param($modulePath, $check, $arguments)
                $env:PSModulePath = $modulePath
                & $check @arguments
                exit $LASTEXITCODE
            } -args $script:ModulePath, $script:Check, $Arguments
        }
    }

    BeforeEach {
        $script:EnvironmentBefore = @{}
        foreach ($name in @('PATH', 'PSModulePath', 'WINSHELL_CHECK_LOG', 'WINSHELL_CHECK_RESULTS', 'WINSHELL_CHECK_FAIL_PESTER', 'WINSHELL_CHECK_FAIL_LINT', 'WINSHELL_CHECK_FAIL_FORMAT')) {
            $script:EnvironmentBefore[$name] = [Environment]::GetEnvironmentVariable($name)
        }
        $script:Stub = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $modules = Join-Path $script:Stub 'modules'
        $pester = Join-Path $modules 'Pester/5.6.1'
        New-Item -ItemType Directory -Path $pester -Force | Out-Null
        $env:WINSHELL_CHECK_LOG = Join-Path $script:Stub 'calls.txt'
        $env:WINSHELL_CHECK_RESULTS = Join-Path $script:Stub 'results'
        $env:WINSHELL_CHECK_FAIL_PESTER = $null
        $env:WINSHELL_CHECK_FAIL_LINT = $null
        $env:WINSHELL_CHECK_FAIL_FORMAT = $null
        foreach ($linter in @('lychee', 'actionlint', 'zizmor')) {
            Set-Content -LiteralPath (Join-Path $script:Stub "$linter.ps1") -Value @'
$tool = [IO.Path]::GetFileNameWithoutExtension($MyInvocation.MyCommand.Name)
"$tool $($args -join ' ')" | Add-Content -LiteralPath $env:WINSHELL_CHECK_LOG
if ($env:WINSHELL_CHECK_FAIL_LINT -eq $tool) { exit 23 }
exit 0
'@
        }
        Write-Trx -Directory $env:WINSHELL_CHECK_RESULTS -Rows @('Ns.Class.Fact=Passed')
        Set-Content -LiteralPath (Join-Path $script:Stub 'dotnet.ps1') -Value @'
"dotnet $($args -join ' ')" | Add-Content -LiteralPath $env:WINSHELL_CHECK_LOG
if ($env:WINSHELL_CHECK_FAIL_FORMAT -and $args[0] -eq 'format') { exit 23 }
if ($args[0] -eq 'test') {
    $dest = $args[[array]::IndexOf($args, '--results-directory') + 1]
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    # Each project tested writes a results file of its own.
    Get-ChildItem -Path (Join-Path $env:WINSHELL_CHECK_RESULTS '*.trx') |
        Copy-Item -Destination (Join-Path $dest ([guid]::NewGuid().ToString('N') + '.trx'))
}
exit 0
'@
        Set-Content -LiteralPath (Join-Path $pester 'Pester.psm1') -Value @'
function Invoke-Pester {
    param([string[]] $Path, [switch] $PassThru)
    "Pester $Path" | Add-Content -LiteralPath $env:WINSHELL_CHECK_LOG
    [pscustomobject]@{ Result = $(if ($env:WINSHELL_CHECK_FAIL_PESTER) { 'Failed' } else { 'Passed' }) }
}
'@
        New-ModuleManifest -Path (Join-Path $pester 'Pester.psd1') -RootModule Pester.psm1 -ModuleVersion 5.6.1 -FunctionsToExport Invoke-Pester
        $env:PATH = "$script:Stub;$env:PATH"
        $script:ModulePath = "$modules;$(Join-Path $PSHOME 'Modules')"
    }

    AfterEach {
        foreach ($name in $script:EnvironmentBefore.Keys) {
            [Environment]::SetEnvironmentVariable($name, $script:EnvironmentBefore[$name])
        }
    }

    It 'builds, formats, and runs the windowless .NET suites and the tooling suite by default' {
        Invoke-Check | Out-Null
        $LASTEXITCODE | Should -Be 0
        $calls = Get-Content -LiteralPath $env:WINSHELL_CHECK_LOG
        $calls[0] | Should -Match '^dotnet build '
        $calls | Should -Contain 'dotnet build scripts/generate-reference.cs --nologo'
        $test = @($calls | Where-Object { $_ -match '^dotnet test ' })
        $test | Should -HaveCount $script:ProjectCount
        $test | ForEach-Object { $_ | Should -Match '--filter Category!=Package&Category!=Window ' }
        $calls[-1] | Should -Be 'Pester tests/Scripts'
    }

    It 'stops before running tests when formatting verification fails' {
        $env:WINSHELL_CHECK_FAIL_FORMAT = '1'
        Invoke-Check 2>&1 | Out-Null
        $LASTEXITCODE | Should -Be 23
        $calls = @(Get-Content -LiteralPath $env:WINSHELL_CHECK_LOG)
        @($calls | Where-Object { $_ -match '^dotnet test |^Pester ' }) | Should -HaveCount 0
    }

    It 'uses an existing build and forwards the seed and hang timeout' {
        Invoke-Check @{ NoBuild = $true; Seed = 7 } | Out-Null
        $LASTEXITCODE | Should -Be 0
        $calls = @(Get-Content -LiteralPath $env:WINSHELL_CHECK_LOG)
        $calls[0] | Should -Not -Match '^dotnet build '
        $test = @($calls | Where-Object { $_ -match '^dotnet test ' })
        $test[0] | Should -Match 'xUnit.Seed=7'
        $test[0] | Should -Match 'xUnit.MaxParallelThreads=2x'
        $test[0] | Should -Match '--blame-hang-timeout 3m'
    }

    It 'builds and runs only <Project>, without Pester' -ForEach @(
        @{ Tier = 'Docs'; Project = 'tests\Winshell.Shell.Tests' }
        @{ Tier = 'Package'; Project = 'tests\Winshell.Package.Tests' }
    ) {
        Remove-Item -LiteralPath (Join-Path $script:Stub 'modules/Pester/5.6.1/Pester.psd1')
        Invoke-Check @{ Tier = $Tier } | Out-Null
        $LASTEXITCODE | Should -Be 0
        $calls = @(Get-Content -LiteralPath $env:WINSHELL_CHECK_LOG)
        $calls | Should -HaveCount 2
        $calls[0] | Should -Be "dotnet build $Project -warnaserror --nologo"
        $calls[1] | Should -Match "^dotnet test \S*\\$([regex]::Escape($Project)) --no-build "
    }

    # A filter that matches nothing, after a renamed namespace or category, ends here.
    It 'fails a <Tier> run that ran no tests' -ForEach @(
        @{ Tier = 'Docs' }
        @{ Tier = 'Package' }
    ) {
        Remove-Item -Path (Join-Path $env:WINSHELL_CHECK_RESULTS '*.trx')
        Write-Trx -Directory $env:WINSHELL_CHECK_RESULTS -Rows @()
        Invoke-Check @{ Tier = $Tier } | Out-Null
        $LASTEXITCODE | Should -Be 1
    }

    It 'fails before building when the pinned Pester version is missing' {
        Remove-Item -LiteralPath (Join-Path $script:Stub 'modules/Pester/5.6.1/Pester.psd1')
        $output = Invoke-Check 2>&1
        $LASTEXITCODE | Should -Not -Be 0
        ($output -join "`n") | Should -Match 'Pester 5.6.1 is required'
        Test-Path -LiteralPath $env:WINSHELL_CHECK_LOG | Should -BeFalse
    }

    It 'fails when the tooling suite fails' {
        $env:WINSHELL_CHECK_FAIL_PESTER = '1'
        Invoke-Check | Out-Null
        $LASTEXITCODE | Should -Be 1
    }

    It 'lints offline without Pester, a build, or tests' {
        Remove-Item -LiteralPath (Join-Path $script:Stub 'modules/Pester/5.6.1/Pester.psd1')
        pwsh -NoProfile -File $script:Check -Lint | Out-Null
        $LASTEXITCODE | Should -Be 0
        $calls = @(Get-Content -LiteralPath $env:WINSHELL_CHECK_LOG)
        $calls | Should -HaveCount 3
        $calls[0] | Should -Match '^lychee .*--offline '
        $calls[1].Trim() | Should -Be 'actionlint'
        $calls[2] | Should -Match '^zizmor .*--offline '
    }

    It 'fails immediately when <Tool> fails' -ForEach @(
        @{ Tool = 'lychee'; Calls = 1 }
        @{ Tool = 'actionlint'; Calls = 2 }
        @{ Tool = 'zizmor'; Calls = 3 }
    ) {
        $env:WINSHELL_CHECK_FAIL_LINT = $Tool
        pwsh -NoProfile -File $script:Check -Lint | Out-Null
        $LASTEXITCODE | Should -Be 23
        @(Get-Content -LiteralPath $env:WINSHELL_CHECK_LOG) | Should -HaveCount $Calls
    }
}
