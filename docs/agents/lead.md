# Lead a set of tasks

A lead thread splits work into tasks and hands each to a delegate: a subagent or another thread.

- Run tasks side by side only when their areas and files don't overlap. `gh pr list --json number,headRefName,files` shows what open PRs touch. A task that runs the test suite or opens a window also needs the one [live session](../development.md#live-ui-with-winland-running), so those tasks run one at a time even when their files don't overlap.
- Keep a one-screen checklist on the tracking issue: merged PRs, open PRs, the next slice, and each child's worktree path. Whoever picks the work up reads that first.
- A delegate leaves auto-merge off and hands its PR to the lead once it is open, or once every layer of its stack is open. The lead reviews the diff and, once the checks pass, merges it by hand with `gh pr merge <number> --squash`, then sees it to `MERGED`.
