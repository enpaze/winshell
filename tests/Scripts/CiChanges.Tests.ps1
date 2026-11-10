BeforeAll {
    $script:Classifier = (Resolve-Path (Join-Path $PSScriptRoot '../../scripts/ci-changes.ps1')).Path
}

Describe 'Selecting CI checks from GitHub changed files' {
    It '<Name>' -ForEach @(
        @{ Name = 'keeps agent prose to link checks'; Path = 'AGENTS.md'; Text = '# Instructions'; Patch = '+A sentence'; Code = $false; Docs = $false },
        @{ Name = 'keeps guide prose to link checks'; Path = 'docs/development.md'; Text = '# Development'; Patch = '+A sentence'; Code = $false; Docs = $false },
        @{ Name = 'runs code checks for a compiled documentation sample'; Path = 'docs/samples/message-widget/Message.xaml.cs'; Text = ''; Patch = '+code'; Code = $true; Docs = $false },
        @{ Name = 'keeps documentation sample prose to link checks'; Path = 'docs/samples/message-widget/README.md'; Text = '# Sample'; Patch = '+A sentence'; Code = $false; Docs = $false },
        @{ Name = 'keeps script documentation out of installer checks'; Path = 'scripts/README.md'; Text = '# Scripts'; Patch = '+A sentence'; Code = $false; Docs = $false },
        @{ Name = 'keeps action documentation out of tooling checks'; Path = '.github/actions/README.md'; Text = '# Actions'; Patch = '+A sentence'; Code = $false; Docs = $false },
        @{ Name = 'keeps an issue form to lint'; Path = '.github/ISSUE_TEMPLATE/agent_task.yml'; Text = 'name: Agent task'; Patch = '+  - type: textarea'; Code = $false; Docs = $false },
        @{ Name = 'keeps a deleted prose page to link checks'; Path = 'docs/privacy.md'; Text = $null; Patch = '-# Privacy'; Code = $false; Docs = $false },
        @{ Name = 'checks a page containing compiled examples'; Path = 'docs/configuration.md'; Text = "``````csharp`nShell.Run();`n``````"; Patch = '+A sentence'; Code = $false; Docs = $true },
        @{ Name = 'checks a new README example'; Path = 'README.md'; Text = "``````csharp`nShell.Run();`n``````"; Patch = '+```csharp'; Code = $false; Docs = $true },
        @{ Name = 'checks a removed last C# fence'; Path = 'README.md'; Text = '# README'; Patch = '-```csharp'; Code = $false; Docs = $true },
        @{ Name = 'checks a deleted example page'; Path = 'docs/example.md'; Text = $null; Patch = '-```csharp'; Code = $false; Docs = $true },
        @{ Name = 'checks generated reference changes'; Path = 'docs/reference/winshell-audio.md'; Text = '# Audio'; Patch = '+A member'; Code = $false; Docs = $true },
        @{ Name = 'checks documentation when GitHub omits a patch'; Path = 'README.md'; Text = '# README'; Patch = $null; Code = $false; Docs = $true },
        @{ Name = 'leaves agent examples outside the application sample contract'; Path = 'docs/agents/domain.md'; Text = '```csharp'; Patch = '+```csharp'; Code = $false; Docs = $false },
        @{ Name = 'runs the suite for application changes'; Path = 'src/Winshell/Program.cs'; Text = ''; Patch = '+code'; Code = $true; Docs = $false }
    ) {
        $checkout = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $checkout | Out-Null
        if ($null -ne $Text) {
            $target = Join-Path $checkout $Path
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            Set-Content -LiteralPath $target -Value $Text
        }
        $json = Join-Path $TestDrive 'changed.json'
        ConvertTo-Json -InputObject @(@{ filename = $Path; patch = $Patch }) | Set-Content -LiteralPath $json
        Push-Location $checkout
        try { $output = & $script:Classifier -Files $json }
        finally { Pop-Location }
        $output | Should -Contain "code=$($Code.ToString().ToLowerInvariant())"
        $output | Should -Contain "docs=$($Docs.ToString().ToLowerInvariant())"
        $output | Should -Contain "powershell=$($Code.ToString().ToLowerInvariant())"
        $output | Should -Contain 'scripts=false'
        $output | Should -Contain 'workflows=false'
        $output | Should -Contain 'package=false'
    }

    It 'retains full checks when a source file is renamed into documentation' {
        $json = Join-Path $TestDrive 'rename.json'
        ConvertTo-Json -InputObject @(@{ filename = 'docs/example.md'; previous_filename = 'src/Example.cs'; patch = '' }) | Set-Content -LiteralPath $json
        $output = & $script:Classifier -Files $json
        $output | Should -Contain 'code=true'
    }

    It 'retains full checks when prose and application code change together' {
        $json = Join-Path $TestDrive 'mixed.json'
        ConvertTo-Json -InputObject @(
            @{ filename = 'AGENTS.md'; patch = '+prose' },
            @{ filename = 'src/Winshell/Program.cs'; patch = '+code' }
        ) | Set-Content -LiteralPath $json
        $output = & $script:Classifier -Files $json
        $output | Should -Contain 'code=true'
    }

    It 'checks examples renamed out of the guide even when the patch changes only prose' {
        $checkout = Join-Path $TestDrive 'moved-example'
        New-Item -ItemType Directory -Path $checkout | Out-Null
        Set-Content -LiteralPath (Join-Path $checkout 'moved.md') -Value '```csharp'
        $json = Join-Path $TestDrive 'moved.json'
        ConvertTo-Json -InputObject @(@{ filename = 'moved.md'; previous_filename = 'docs/example.md'; patch = '+prose' }) | Set-Content -LiteralPath $json
        Push-Location $checkout
        try { $output = & $script:Classifier -Files $json }
        finally { Pop-Location }
        $output | Should -Contain 'code=false'
        $output | Should -Contain 'docs=true'
    }

    # Flags lists the flags a path sets, and every other flag is false: code runs the test shards, powershell the formatting
    # check and the PowerShell suites in quality, scripts the installer job, workflows actionlint, and package the Package
    # workflow.
    It '<Path> selects <Flags>' -ForEach @(
        @{ Path = 'skills-lock.json'; Flags = 'none' },
        @{ Path = '.agents/skills/wizard/template.sh'; Flags = 'none' },
        @{ Path = '.agents/skills/tdd/agents/openai.yaml'; Flags = 'none' },
        @{ Path = '.claude/skills/wizard/template.sh'; Flags = 'none' },
        @{ Path = '.claude/skills/tdd/agents/openai.yaml'; Flags = 'none' },
        @{ Path = 'LICENSE'; Flags = 'none' },
        @{ Path = '.gitignore'; Flags = 'none' },
        @{ Path = 'scripts/uninstall.ps1'; Flags = 'powershell scripts' },
        @{ Path = 'scripts/pr-rules.ps1'; Flags = 'powershell scripts' },
        @{ Path = 'tests/Scripts/Install.Tests.ps1'; Flags = 'powershell scripts' },
        @{ Path = '.github/workflows/pr-title.yml'; Flags = 'powershell workflows' },
        @{ Path = '.github/dependabot.yml'; Flags = 'powershell workflows' },
        @{ Path = '.github/workflows/package.yml'; Flags = 'powershell workflows package' },
        @{ Path = 'packaging/msix/AppxManifest.xml'; Flags = 'package' },
        @{ Path = 'packaging/signpath/artifact-configuration.xml'; Flags = 'package' },
        # The suite reads these, so they are code although their folders are not.
        @{ Path = 'scripts/install.ps1'; Flags = 'code powershell scripts' },
        @{ Path = 'packaging/winshell.ico'; Flags = 'code powershell package' },
        @{ Path = 'packaging/msix/Assets/StoreLogo.png'; Flags = 'code powershell package' },
        # The suite's own machinery.
        @{ Path = 'scripts/lib/Tests.ps1'; Flags = 'code powershell scripts package' },
        @{ Path = 'scripts/build-template.ps1'; Flags = 'code powershell scripts' },
        @{ Path = 'scripts/generate-reference.cs'; Flags = 'code powershell scripts' },
        @{ Path = '.github/workflows/ci.yml'; Flags = 'code powershell scripts workflows' },
        @{ Path = '.github/actions/prepare/action.yml'; Flags = 'code powershell scripts workflows package' },
        # Everything else.
        @{ Path = 'src/Winshell.Shell/BuiltInStyle.xaml'; Flags = 'code powershell' },
        @{ Path = 'tests/Winshell.Tests/AppIconTests.cs'; Flags = 'code powershell' },
        @{ Path = 'examples/omarchy-style/TokyoNight.xaml'; Flags = 'code powershell' },
        @{ Path = 'docs/samples/message-widget/Message.xaml.cs'; Flags = 'code powershell' },
        @{ Path = 'templates/winshell/Program.cs'; Flags = 'code powershell package' },
        @{ Path = 'src/Winshell.Shell/Winshell.Shell.csproj'; Flags = 'code powershell package' },
        @{ Path = 'Directory.Build.props'; Flags = 'code powershell package' },
        @{ Path = 'NOTICE'; Flags = 'code powershell' }
    ) {
        $json = Join-Path $TestDrive 'kind.json'
        ConvertTo-Json -InputObject @(@{ filename = $Path; patch = '' }) | Set-Content -LiteralPath $json
        $output = & $script:Classifier -Files $json
        foreach ($flag in 'code', 'powershell', 'scripts', 'workflows', 'package') {
            $output | Should -Contain "$flag=$(($Flags -split ' ' -contains $flag).ToString().ToLowerInvariant())"
        }
    }

    It 'selects the checks of each kind in a change of several kinds' {
        $json = Join-Path $TestDrive 'kinds.json'
        ConvertTo-Json -InputObject @(
            @{ filename = 'scripts/uninstall.ps1'; patch = '' },
            @{ filename = '.github/workflows/pr-title.yml'; patch = '' },
            @{ filename = 'packaging/msix/AppxManifest.xml'; patch = '' }
        ) | Set-Content -LiteralPath $json
        $output = & $script:Classifier -Files $json
        $output | Should -Contain 'code=false'
        $output | Should -Contain 'powershell=true'
        $output | Should -Contain 'scripts=true'
        $output | Should -Contain 'workflows=true'
        $output | Should -Contain 'package=true'
    }

    It '<Name>' -ForEach @(
        @{ Name = 'packs a template change'; Path = 'templates/winshell/Program.cs'; Package = $true },
        @{ Name = 'packs a library project file change'; Path = 'src/Winshell.Shell/Winshell.Shell.csproj'; Package = $true },
        @{ Name = 'packs a central package version change'; Path = 'Directory.Packages.props'; Package = $true },
        @{ Name = 'packs a change to the script that runs the package tests'; Path = 'scripts/lib/Tests.ps1'; Package = $true },
        @{ Name = 'packs a change to the support the package tests compile'; Path = 'tests/TestSupport/WinshellCli.cs'; Package = $true },
        @{ Name = 'packs a release workflow change'; Path = '.github/workflows/release-please.yml'; Package = $true },
        @{ Name = 'packs a change to an action the package jobs use'; Path = '.github/actions/prepare/action.yml'; Package = $true },
        @{ Name = 'leaves library source to the main checks'; Path = 'src/Winshell.Shell/Panel.cs'; Package = $false },
        @{ Name = 'leaves template prose to link checks'; Path = 'templates/README.md'; Package = $false }
    ) {
        $json = Join-Path $TestDrive 'package.json'
        ConvertTo-Json -InputObject @(@{ filename = $Path; patch = '' }) | Set-Content -LiteralPath $json
        & $script:Classifier -Files $json | Should -Contain "package=$($Package.ToString().ToLowerInvariant())"
    }

    It 'runs everything for an empty or potentially truncated comparison' -ForEach @(
        @{ Count = 0 }, @{ Count = 300 }
    ) {
        $json = Join-Path $TestDrive 'incomplete.json'
        $files = @(for ($i = 0; $i -lt $Count; $i++) { @{ filename = "docs/$i.md"; patch = '+prose' } })
        ConvertTo-Json -InputObject $files | Set-Content -LiteralPath $json
        $output = & $script:Classifier -Files $json -MaxFiles 300
        $output | Should -Contain 'code=true'
        $output | Should -Contain 'powershell=true'
        $output | Should -Contain 'scripts=true'
        $output | Should -Contain 'workflows=true'
        $output | Should -Contain 'package=true'
    }
}
