# The versions, hashes and waits of the tools CI installs by URL live in one file.
# A bump edits that file and nothing else copies the numbers.
BeforeAll {
    $script:Root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    $script:Pin = 'scripts/tool-pins.psd1'
    $script:Pins = Import-PowerShellDataFile -LiteralPath (Join-Path $script:Root $script:Pin)

    # git grep exits 1 when nothing matches. That is an empty result, not a failed search.
    function script:Get-OtherPinHits([string] $Needle) {
        if (-not $Needle) { throw 'A pin needle is empty.' }
        $output = git -C $script:Root grep -F -I -n -e $Needle -- .
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
    It 'keeps every version and hash in scripts/tool-pins.psd1 alone' {
        $needles = @(
            $script:Pins.lychee.version
            $script:Pins.lychee.windowsSha256
            $script:Pins.lychee.linuxSha256
            $script:Pins.actionlint.version
            $script:Pins.zizmor.version
            $script:Pins.skills.version
            $script:Pins.pester.version
            $script:Pins.pester.sha256
            $script:Pins.wingetCreate.url
            $script:Pins.wingetCreate.sha256
            $script:Pins.wingetUtil.url
            $script:Pins.wingetUtil.sha256
            # The versions sit in those URLs. They are not a second copy of the download.
            ([regex]::Match($script:Pins.wingetCreate.url, '/v(?<version>\d+\.\d+\.\d+\.\d+)/')).Groups['version'].Value
            ([regex]::Match($script:Pins.wingetUtil.url, '/(?<version>\d+\.\d+\.\d+)/')).Groups['version'].Value
        )
        $copied = foreach ($needle in $needles) {
            foreach ($path in (Get-OtherPinHits $needle)) { "$needle in $path" }
        }
        $copied | Should -BeNullOrEmpty
    }

    It 'is what the workflows, check, the skills update and packaging read' {
        foreach ($relative in @(
                'scripts/check.ps1'
                'scripts/update-skills.ps1'
                'scripts/package.ps1'
                '.github/workflows/ci.yml'
                '.github/workflows/links.yml'
                '.github/workflows/package.yml'
                '.github/actions/powershell/action.yml'
            )) {
            $text = Get-Content -Raw -LiteralPath (Join-Path $script:Root $relative)
            $text | Should -Match 'tool-pins\.psd1' -Because $relative
        }
    }

    It 'lets the actionlint job export the pin, because those versions are action inputs' {
        $ci = Get-Content -Raw -LiteralPath (Join-Path $script:Root '.github/workflows/ci.yml')
        $ci | Should -Match 'steps\.pins\.outputs\.actionlint'
        $ci | Should -Match 'steps\.pins\.outputs\.zizmor'
    }

    It 'does not copy the lychee waits or the skills quiet days out of the pin' {
        $links = Get-Content -Raw -LiteralPath (Join-Path $script:Root '.github/workflows/links.yml')
        $ci = Get-Content -Raw -LiteralPath (Join-Path $script:Root '.github/workflows/ci.yml')
        $update = Get-Content -Raw -LiteralPath (Join-Path $script:Root 'scripts/update-skills.ps1')
        $links | Should -Not -Match 'retry-wait-time \d+'
        $links | Should -Not -Match 'max-retries \d+'
        $ci | Should -Not -Match 'RetryIntervalSec \d+'
        $ci | Should -Not -Match 'MaximumRetryCount \d+'
        $update | Should -Not -Match 'AddDays\(-\d+\)'
        $update | Should -Match 'quietDays'
    }

    It 'is the file docs/development.md names instead of the numbers' {
        $docs = Get-Content -Raw -LiteralPath (Join-Path $script:Root 'docs/development.md')
        $docs | Should -Match ([regex]::Escape($script:Pin))
        foreach ($needle in @(
                $script:Pins.lychee.version
                $script:Pins.actionlint.version
                $script:Pins.zizmor.version
                $script:Pins.skills.version
                $script:Pins.pester.version
            )) {
            $docs | Should -Not -Match ([regex]::Escape($needle)) -Because $needle
        }
    }
}
