<!--
Read CONTRIBUTING.md before opening a PR. Solve one underlying problem.
Title: a conventional commit in plain language, such as "fix(api): retries no longer duplicate orders".
Branch: <type>/<short-description>, such as "fix/duplicate-orders", or agent/<tool>/<issue#>-<slug> for an AI agent.
An AI agent writes this body with the pr skill.
-->

## Summary

<!-- The smallest diagram, diff sketch or tree that makes the change clear, next to the short text it supports. Skip
preambles and keep prose brief. Split independent fixes into their own PRs. -->

Fixes #

<!-- The issue it fixes after "Fixes #". With no issue, delete that line and say why this needs none, such as a small fix
of an obvious bug. -->

## Evidence

<!-- Concrete evidence that the change works, as a before and after: a screenshot for a visual change, otherwise the exact
test or output that failed and now passes. "Tests pass" is not evidence. Repo-wide checks you ran for every PR need no
mention. Upload evidence to GitHub; never commit PR-only assets to main.
A fix adds a test and says here how it failed before the fix, or says why it can't have one on a "**No test:**" line.
A perf change gives a number for each, here or in a table with Before and After headings.
A feat, fix or perf change to XAML in src/ shows a winshell inspect screenshot, or says why not on a "**Not visual:**"
line. pwsh scripts/pr-screenshot.ps1 <png> publishes one and prints the Markdown to paste. -->

- **Before:**
  **After:**

**Not verified:** <!-- Anything you could not check. -->

## Merge Danger

**Door:** <!-- One-way or two-way: can this be walked back? -->

**Blast Radius:** <!-- One word for what this can affect, then any ramifications. -->

**Agent:** <!-- If an AI agent wrote this PR, the model and the harness, such as "Claude Opus 5.5 in Claude Code".
Otherwise delete this line. -->
