# The versions, hashes and waits of the tools CI installs by URL live in one file.
# A bump edits that file and nothing else copies the numbers.
BeforeAll {
    $script:Root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    $script:Pin = 'scripts/tool-pins.psd1'
    $script:Pins = Import-PowerShellDataFile -LiteralPath (Join-Path $script:Root $script:Pin)
    $script:Readers = @(
        'scripts/check.ps1'
        'scripts/package.ps1'
        '.github/workflows/ci.yml'
        '.github/workflows/links.yml'
        '.github/workflows/package.yml'
        '.github/actions/powershell/action.yml'
    )

    # git grep exits 1 when nothing matches. That is an empty result, not a failed search.
    function script:Get-OtherPinHits([string] $Needle, [string[]] $Paths = @('.')) {
        if (-not $Needle) { throw 'A pin needle is empty.' }
        $output = git -C $script:Root grep -F -I -n -e $Needle -- @Paths
        if ($LASTEXITCODE -eq 1) { return @() }
        if ($LASTEXITCODE -ne 0) { throw "git grep failed while looking for $Needle" }
        @(
            $output |
                ForEach-Object { ($_ -split ':', 3)[0] -replace '\\', '/' } |
                Where-Object { $_ -ne $script:Pin } |
                Select-Object -Unique
        )
    }
}

Describe 'The tools CI installs by URL' {
    # A hash or a URL names one download anywhere in the repository. A bare version such as 1.7.6 can also be some
    # other package's, so it is looked for only where the pins are read.
    It 'keeps every version and hash in scripts/tool-pins.psd1 alone' {
        $anywhere = @(
            $script:Pins.lychee.windowsSha256
            $script:Pins.lychee.linuxSha256
            $script:Pins.pester.sha256
            $script:Pins.wingetCreate.url
            $script:Pins.wingetCreate.sha256
            $script:Pins.wingetUtil.url
            $script:Pins.wingetUtil.sha256
        )
        $inReaders = @(
            $script:Pins.lychee.version
            $script:Pins.actionlint.version
            $script:Pins.zizmor.version
            $script:Pins.pester.version
            # The versions sit in those URLs. They are not a second copy of the download.
            ([regex]::Match($script:Pins.wingetCreate.url, '/v(?<version>\d+\.\d+\.\d+\.\d+)/')).Groups['version'].Value
            ([regex]::Match($script:Pins.wingetUtil.url, '/(?<version>\d+\.\d+\.\d+)/')).Groups['version'].Value
        )
        $copied = @(
            foreach ($needle in $anywhere) {
                foreach ($path in (Get-OtherPinHits $needle)) { "$needle in $path" }
            }
            foreach ($needle in $inReaders) {
                foreach ($path in (Get-OtherPinHits $needle ($script:Readers + 'docs/development.md'))) { "$needle in $path" }
            }
        )
        $copied | Should -BeNullOrEmpty
    }

    It 'is what the workflows, check and packaging read' {
        foreach ($relative in $script:Readers) {
            $text = Get-Content -Raw -LiteralPath (Join-Path $script:Root $relative)
            $text | Should -Match 'tool-pins\.psd1' -Because $relative
        }
    }

    It 'lets the actionlint job export the pin, because those versions are action inputs' {
        $ci = Get-Content -Raw -LiteralPath (Join-Path $script:Root '.github/workflows/ci.yml')
        $ci | Should -Match 'steps\.pins\.outputs\.actionlint'
        $ci | Should -Match 'steps\.pins\.outputs\.zizmor'
    }

    It 'does not copy the lychee waits out of the pin' {
        $links = Get-Content -Raw -LiteralPath (Join-Path $script:Root '.github/workflows/links.yml')
        $ci = Get-Content -Raw -LiteralPath (Join-Path $script:Root '.github/workflows/ci.yml')
        $links | Should -Not -Match 'retry-wait-time \d+'
        $links | Should -Not -Match 'max-retries \d+'
        $ci | Should -Not -Match 'RetryIntervalSec \d+'
        $ci | Should -Not -Match 'MaximumRetryCount \d+'
    }

    It 'is the file docs/development.md names instead of the numbers' {
        $docs = Get-Content -Raw -LiteralPath (Join-Path $script:Root 'docs/development.md')
        $docs | Should -Match ([regex]::Escape($script:Pin))
        foreach ($needle in @(
                $script:Pins.lychee.version
                $script:Pins.actionlint.version
                $script:Pins.zizmor.version
                $script:Pins.pester.version
            )) {
            $docs | Should -Not -Match ([regex]::Escape($needle)) -Because $needle
        }
    }
}
