#requires -Version 7.0
# Takes the newer version of each skill in .agents/skills, from every source whose newest commit is at least a week old,
# and writes the body of the pull request that holds the change. The skills workflow runs it weekly:
#
#   pwsh scripts/update-skills.ps1 -BodyPath <file>
#
# It writes no body when no skill changed. Needs gh signed in, npx, and the history of skills-lock.json.
param([Parameter(Mandatory)][string] $BodyPath)

$ErrorActionPreference = 'Stop'
$BodyPath = [IO.Path]::GetFullPath($BodyPath)
Set-Location (Join-Path $PSScriptRoot '..')

# The committed lock, so a second run compares with the same versions as the first.
$lock = (git show HEAD:skills-lock.json | ConvertFrom-Json -AsHashtable).skills
$cutoff = [DateTimeOffset]::UtcNow.AddDays(-7)

# Wait a week, as dependabot.yml does, so a compromised commit is usually caught first.
$names = foreach ($source in $lock.Values.source | Sort-Object -Unique) {
    $newest = [DateTimeOffset] (gh api "repos/$source/commits?per_page=1" --jq '.[0].commit.committer.date')
    if ($LASTEXITCODE) { throw "gh could not read the commits of $source." }
    if ($newest -gt $cutoff) {
        Write-Host "Leaving $source alone: its newest commit, from $($newest.ToString('u')), is younger than 7 days."
        continue
    }
    $lock.Keys | Where-Object { $lock[$_].source -eq $source } | Sort-Object
}
if (-not $names) { return }

npx -y skills@1.7.6 update @names -p -y | Out-Host
if ($LASTEXITCODE) { throw "skills update failed with exit code $LASTEXITCODE." }

$updated = (Get-Content -Raw -LiteralPath skills-lock.json | ConvertFrom-Json -AsHashtable).skills
$changed = $lock.Keys | Where-Object { $updated[$_].computedHash -ne $lock[$_].computedHash } | Sort-Object
if (-not $changed) { return }

$body = @(
    'Takes the newer version of each skill below. Read the diff before you take it: agents follow these skills with full permissions.'
    foreach ($name in $changed) {
        $skill = $lock[$name]
        $folder = $skill.skillPath -replace '/[^/]+$'
        # The repo took the version it has when its hash entered the lock, so every later commit of the source is new.
        $taken = git log -S $skill.computedHash --reverse --format=%cI -- skills-lock.json | Select-Object -First 1
        $since = ([DateTimeOffset] $taken).UtcDateTime.ToString('yyyy-MM-ddTHH:mm:ssZ')
        $commits = gh api --paginate "repos/$($skill.source)/commits?path=$folder&since=$since" --jq '.[] | .sha[0:7] + " " + (.commit.message | split("\n")[0])'
        if ($LASTEXITCODE) { throw "gh could not read the commits of $($skill.source)." }
        ''
        "### $name"
        ''
        "From ``$($skill.source)``, ``$folder``, which gained:"
        ''
        foreach ($commit in $commits) {
            $sha, $subject = $commit -split ' ', 2
            # A code span links no issue and notifies no one it mentions, here or in the source.
            "- $($skill.source)@$sha ``$($subject -replace '`', "'")``"
        }
    }
)
Set-Content -LiteralPath $BodyPath -Value $body
