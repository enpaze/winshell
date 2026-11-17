BeforeAll {
    $script:Script = (Resolve-Path (Join-Path $PSScriptRoot '../../scripts/comments.ps1')).Path

    function Invoke-Comments {
        param([switch] $VerifyNoChanges)
        $output = pwsh -NoProfile -File $script:Script -Root $script:Fixture -VerifyNoChanges:$VerifyNoChanges 2>&1
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
    }

    function Write-Fixture([string] $Path, [string[]] $Lines) {
        $full = Join-Path $script:Fixture $Path
        New-Item -ItemType Directory -Path (Split-Path -Parent $full) -Force | Out-Null
        [IO.File]::WriteAllText($full, (Join-Lines $Lines))
    }

    function Join-Lines([string[]] $Lines) {
        ($Lines -join "`n") + "`n"
    }

    function Read-Fixture([string] $Path) {
        [IO.File]::ReadAllText((Join-Path $script:Fixture $Path))
    }
}

Describe 'Comment removal' {
    BeforeEach {
        $script:Fixture = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:Fixture | Out-Null
        git -C $script:Fixture init --quiet
        if ($LASTEXITCODE -ne 0) { throw 'Could not initialize fixture repository.' }
        Write-Fixture 'src/A.cs' @(
            '/// <summary>Doc.</summary>'
            'public static class A'
            '{'
            '    // Leading.'
            '    public const int X = 1; // Trailing.'
            ''
            '    // Alone between blank lines.'
            ''
            '    public static int Y(int v) => v /* inline */ + 1;'
            '#pragma warning disable CA1031 // Reason.'
            '    public const string Url = "https://example.com"; '
            '}')
        Write-Fixture 'scripts/s.ps1' @(
            '#requires -Version 7.0'
            '<#'
            '.SYNOPSIS'
            '    Help.'
            '#>'
            'param()'
            '# Note.'
            "'#not a comment' # Trailing.")
        Write-Fixture '.github/workflows/w.yml' @(
            'on: push # Trigger.'
            'jobs:'
            '  a:'
            '    steps:'
            '      - uses: ./x # zizmor: ignore[self-repository]'
            '      # Step note.'
            '      - run: |'
            '          # Shell note.'
            "          echo '#kept'"
            '      - with:'
            '          body: |'
            '            # Heading')
        Write-Fixture 'p.csproj' @(
            '<Project>'
            '  <!-- Note. -->'
            '  <PropertyGroup />'
            '</Project>')
    }

    It 'lists each comment and fails, then removes them and passes' {
        $check = Invoke-Comments -VerifyNoChanges
        $check.ExitCode | Should -Be 1 -Because $check.Output
        $check.Output | Should -Match ([regex]::Escape('src/A.cs:1: /// <summary>Doc.</summary>'))
        $check.Output | Should -Match ([regex]::Escape('src/A.cs:5: // Trailing.'))
        $check.Output | Should -Match ([regex]::Escape('scripts/s.ps1:7: # Note.'))
        $check.Output | Should -Match ([regex]::Escape('.github/workflows/w.yml:8: # Shell note.'))
        $check.Output | Should -Match ([regex]::Escape('p.csproj:2: <!-- Note. -->'))
        $check.Output | Should -Not -Match 'zizmor|requires|Heading|#kept|#not a comment|example\.com'

        $strip = Invoke-Comments
        $strip.ExitCode | Should -Be 0 -Because $strip.Output

        Read-Fixture 'src/A.cs' | Should -BeExactly (Join-Lines @(
                'public static class A'
                '{'
                '    public const int X = 1;'
                ''
                '    public static int Y(int v) => v + 1;'
                '#pragma warning disable CA1031'
                '    public const string Url = "https://example.com"; '
                '}'))
        Read-Fixture 'scripts/s.ps1' | Should -BeExactly (Join-Lines @(
                '#requires -Version 7.0'
                'param()'
                "'#not a comment'"))
        Read-Fixture '.github/workflows/w.yml' | Should -BeExactly (Join-Lines @(
                'on: push'
                'jobs:'
                '  a:'
                '    steps:'
                '      - uses: ./x # zizmor: ignore[self-repository]'
                '      - run: |'
                "          echo '#kept'"
                '      - with:'
                '          body: |'
                '            # Heading'))
        Read-Fixture 'p.csproj' | Should -BeExactly (Join-Lines @(
                '<Project>'
                '  <PropertyGroup />'
                '</Project>'))

        $recheck = Invoke-Comments -VerifyNoChanges
        $recheck.ExitCode | Should -Be 0 -Because $recheck.Output
    }
}
