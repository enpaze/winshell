# Issue tracker: GitHub

Issues and specs live in GitHub Issues for `enpaze/winshell`.
Use the `gh` CLI from this repo.

## Conventions

- Before creating an issue, verify the target with
  `gh repo view --json nameWithOwner`. It must be `enpaze/winshell`.
- Create: `gh issue create --title "..." --body-file <path>`.
  Use a temporary file for multiline bodies.
- Read: `gh issue view <number> --json number,title,body,labels,comments`.
- List: `gh issue list --state open --json number,title,body,labels`.
  Add label filters as needed.
- Comment: `gh issue comment <number> --body-file <path>`.
- Labels: `gh issue edit <number> --add-label "..."` or `--remove-label "..."`.
- Close: post the explanation as a comment, then `gh issue close <number>`.
- Use native GitHub sub-issues and issue dependencies when supported.
  Otherwise, put `Part of #<parent>` and `Blocked by: #<number>` at the
  top of the child body, and link children in the parent's task list.
  A ticket is unblocked when every blocker is closed.

When a skill says to publish to the issue tracker, create a GitHub issue.
When it says to fetch a ticket, read the GitHub issue.

## Pull requests as a triage surface

**PRs as a request surface: no.**
