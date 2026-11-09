# Issue tracker: GitHub

Specs and tickets for this repo live as GitHub issues in enpaze/winshell. Use the `gh` CLI, 2.94 or later, for all operations; it infers the repo inside a clone.

## Conventions

- **Create an issue**: `gh issue create --title "..." --body "..."`. Use a heredoc for multi-line bodies.
- **Read an issue**: `gh issue view <number> --json number,title,body,labels,comments`.
- **List issues**: `gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'` with appropriate `--label` and `--state` filters.
- **Make an issue a sub-issue of a parent**: `gh issue create --parent <parent> ...`, or `gh issue edit <parent> --add-sub-issue <child>` afterwards.
- **Blocking**: GitHub's native issue dependencies. Add an edge with `gh issue create --blocked-by <n>,<n> ...` or `gh issue edit <child> --add-blocked-by <blocker>`. A ticket is unblocked when every blocker is closed.
- **Count open blockers**: `gh api repos/{owner}/{repo}/issues/<n> --jq .issue_dependencies_summary.blocked_by` (open blockers only; 0 means startable). `gh issue view <n> --json blockedBy` also lists closed blockers, so filter it: `--jq '[.blockedBy.nodes[] | select(.state == "OPEN")] | length'`.
- **Comment on an issue**: `gh issue comment <number> --body "..."`
- **Apply / remove labels**: `gh issue edit <number> --add-label "..."` / `--remove-label "..."`
- **Close**: post the explanation with `gh issue comment <number> --body "..."`, then `gh issue close <number>`. On an already-closed issue, `gh issue close --comment` drops the comment and still exits 0.

## Pull requests as a triage surface

**PRs as a request surface: no.** _(`/triage` reads this flag.)_

## When a skill says "publish to the issue tracker"

Create a GitHub issue.

A spec gets `ready-for-agent`.

A ticket is a sub-issue of its spec, with a native blocked-by link to each ticket that blocks it. It gets `ready-for-agent` and one `area:` label, the area it changes; a test or a doc page belongs to the area it covers. Its body has the sections `/to-tickets` writes, which the Agent task form in `.github/ISSUE_TEMPLATE/agent_task.yml` also asks for: what to build, acceptance criteria and blocked by. Leave out the blocked-by section when every blocker is a link.

## When a skill says "fetch the relevant ticket"

Read it as in **Read an issue** above.
