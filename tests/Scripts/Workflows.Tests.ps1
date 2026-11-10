Describe 'The workflows' {
    BeforeAll {
        $script:Workflows = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '../../.github/workflows') -File
    }

    It 'run each job on a pinned runner image, never a moving -latest one' {
        foreach ($workflow in $script:Workflows) {
            Get-Content -Raw -LiteralPath $workflow.FullName | Should -Not -Match '-latest' -Because $workflow.Name
        }
    }

    It 'give wingetcreate its token through the environment, never on its command line' {
        $package = Get-Content -Raw -LiteralPath ($script:Workflows | Where-Object Name -EQ 'package.yml').FullName
        $package | Should -Match ([regex]::Escape('WINGET_CREATE_GITHUB_TOKEN: ${{ secrets.WINGET_TOKEN }}'))
        $package | Should -Not -Match '--token'
    }

    It 'copy the skills into .claude/skills, commit only both folders and their lock in a skills update, and never merge it' {
        $skills = Get-Content -Raw -LiteralPath ($script:Workflows | Where-Object Name -EQ 'skills.yml').FullName
        $skills | Should -Match ('(?s)' + [regex]::Escape('scripts/copy-skills.ps1') + '.*' + [regex]::Escape('git add -- .agents/skills .claude/skills skills-lock.json'))
        $skills | Should -Not -Match '(?m)git add (?!-- \.agents/skills \.claude/skills skills-lock\.json$)'
        $skills | Should -Match ([regex]::Escape('git diff --cached --quiet "origin/$BRANCH" -- .agents/skills .claude/skills skills-lock.json'))
        $skills | Should -Not -Match 'gh pr merge|--auto|commit -a'
    }

    It 'fail the required ci check when .claude/skills is not a copy of .agents/skills' {
        $ci = Get-Content -Raw -LiteralPath ($script:Workflows | Where-Object Name -EQ 'ci.yml').FullName
        $ci | Should -Match ([regex]::Escape('./scripts/copy-skills.ps1 -VerifyNoChanges'))
    }
}
