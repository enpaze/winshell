# Winshell

## How we build

I like ambitious ideas, simple systems, and software that feels obvious. Do not preserve complexity just because it already exists. Do not introduce machinery because it looks architecturally impressive. Understand the real constraint, then fight for the smallest model that makes the correct behavior unsurprising.

Channel both "measure twice, cut once" and "yagni". Fight scope creep. Try to honor the dev's intent in both a minimal and realistic fashion.

When the documentation and existing code conflict, follow the code's conventions, and in your final reply, list all the inconsistencies you found.

## Start a task

1. Run `git fetch origin main` and `git rev-list --left-right --count HEAD...origin/main`. A second count above 0 means this checkout, and this file, are behind `main`: update the base as [Start a task](docs/development.md#start-a-task) describes before you edit or rely on them.
2. Install the agent skills with `pwsh scripts/install-skills.ps1`, and use the installed copies.
3. One task is one thread, one issue, one worktree, one branch and one small PR. If the task has no issue, file one with the Agent task form in `.github/ISSUE_TEMPLATE/agent_task.yml` and its `area:*` labels.
4. Many agents work here at once: check `gh pr list --json number,headRefName,files`, and keep your files apart from other open PRs outside your own stack.

## Stack

Scripts are PowerShell 7; `scripts/install.ps1` and `scripts/uninstall.ps1` also run on Windows PowerShell 5.1. [Project structure](docs/development.md#project-structure) maps the folders.

## Before you push

- A test you write, change or review passes the installed `test-audit` skill and [Where a test belongs](CONTRIBUTING.md#where-a-test-belongs).
- Run the [checks for the change](docs/development.md#checks-by-change). Anything that opens a window, the full suite included, follows [Live UI with Winland running](docs/development.md#live-ui-with-winland-running).
- Before opening the PR, get a fresh review: a subagent, or a second pass of your own, reads the diff against this file, [docs/architecture.md](docs/architecture.md) and the `test-audit` skill. Fix what it finds and review again, until it finds nothing.
- Open the PR as [Open a PR](CONTRIBUTING.md#open-a-pr) describes, and own it until `gh pr view <number> --json state --jq .state` prints `MERGED`. A delegate hands it to its lead instead.

## When you

- Resolve a conflict, merge, or change a shared file (a build file, `tests/TestSupport/`, a lockfile, a type more than one module uses): [Conflicts and merging](CONTRIBUTING.md#conflicts-and-merging).
- Build related work as a stack: [Pull request stacks](docs/development.md#pull-request-stacks).
- Lead several tasks, or work as a delegate: [Lead a set of tasks](docs/agents/lead.md).
- Change something visible: [See what you changed](docs/development.md#see-what-you-changed).

## Agent skills

Issues live in GitHub Issues for enpaze/winshell, used through `gh`: see the [issue tracker](docs/agents/issue-tracker.md) and [triage labels](docs/agents/triage-labels.md). Name things with the words of `GLOSSARY.md`: see [domain docs](docs/agents/domain.md).
