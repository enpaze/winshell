# Lead a spec

A lead thread works a spec's tickets through to merged PRs, handing each ticket to a delegate: a subagent or another thread. [The main flow](flow.md) says where the spec and its tickets come from.

## Work the spec

1. Read the spec's progress on GitHub. Its tickets are its sub-issues, and their blocked-by links say which are unblocked: `gh issue view <spec> --json subIssues` lists them with their state, and [the issue tracker page](issue-tracker.md) counts a ticket's open blockers. Keep no checklist of your own.
2. Delegate each unblocked ticket with the brief below, within [the limits](#how-many-at-once).
3. A delegate leaves auto-merge off and hands its PR back once it is open, or once every layer of its stack is open. Review the diff and, once the checks pass, merge it by hand with `gh pr merge <number> --squash`, then see it to `MERGED`.
4. Delegate the tickets that merge unblocked. When the spec's last ticket merges, `/retro` runs with the owner.

## The brief

```text
Follow the `implement` skill for #<ticket>. Hand the PR back to me.
```

The ticket and the repo's documents hold everything else. A third line is only for something true of that one run, such as the branch a stacked ticket builds on.

## A stale ticket

When a ticket's design has changed since it was written, fix it by editing its body before you delegate it, never in a comment or the brief. The delegate starts from a fresh context, and the ticket is all it reads.

A delegate that finds its ticket unclear or wrong stops and reports what it found. Edit the ticket's body and delegate it again, or take it to the owner when the fix is a design decision.

## How many at once

- At most four delegates run at once.
- At most one of them has a ticket that opens windows locally, because those share the one [live session](../development.md#live-ui-with-winland-running). Delegate such tickets one after another.
- Run tickets side by side only when their areas and files don't overlap. Each ticket's `area:*` label names its area, and `gh pr list --json number,headRefName,files` shows what open PRs touch.
