# Lead a spec

A lead thread works a spec's tickets through to merged PRs, handing each ticket to a delegate: a subagent or another thread. [The main flow](flow.md) says where the spec and its tickets come from.

## Work the spec

1. Read the spec's progress on GitHub, from its sub-issues: `gh issue view <spec> --json subIssues` lists its tickets with their state, and [the issue tracker page](issue-tracker.md#conventions) gives the command that counts a ticket's open blockers.
2. Delegate each unblocked ticket with the brief below, within [the limits](#how-many-at-once). Use the role and user-selected model and effort described in [How we build](../../AGENTS.md#how-we-build). A ticket whose one open blocker has an open PR may be delegated too, stacked on that PR's branch.
3. A delegate leaves auto-merge off and hands its PR back once it is open. Review the diff and, once the checks pass, merge it by hand with `gh pr merge <number> --squash`, or a ready stack with `gh stack merge <stack-number> --squash`. Watching a PR wakes you when its required checks pass, which can come before its last check finishes, and never when it merges. If a check is still running and you leave the PR to auto-merge, tell the owner you will not hear the merge. Then see it through [Until it merges](../../CONTRIBUTING.md#until-it-merges).
4. Delegate the tickets each merge unblocks, until every ticket of the spec is closed.
5. When the last ticket merges, run `/retro` with the owner, as [the main flow](flow.md#where-this-repo-overrides-a-skill) says. Each finding the owner accepts becomes a ticket.

## The brief

```text
Role: Builder
Responsibilities and limits: <copy the Builder responsibilities and limits from AGENTS.md>
Follow the `implement` skill for #<ticket>. Hand the PR back to me.
```

The ticket and the repo's documents hold everything else. Add only facts specific to that run, such as the branch a stacked ticket builds on.

## A stale ticket

When a ticket's design has changed since it was written, fix it by editing its body before you delegate it, never in a comment or the brief. The ticket alone has to carry the design, for this delegate and the next.

A delegate that finds its ticket unclear or wrong reports back instead of building it, as [the main flow](flow.md#who-runs-each-step) says. Edit the ticket's body and delegate it again, or take it to the owner when the fix is a design decision.

## How many at once

- At most four delegates run at once.
- At most one of them has a ticket that runs a windowed test or a live shell on purpose, because those share the one [live session](../development.md#live-ui-with-winland-running). The full check doesn't count: it runs only the tests that open no windows, and CI runs the windowed ones.
- Run tickets side by side only when their areas and files don't overlap. Each ticket's `area:*` label names its area, and `gh pr list --json number,headRefName,files` shows what open PRs touch.
