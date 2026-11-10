# Winshell

## How we build

I like ambitious ideas, simple systems, and software that feels obvious. Do not preserve complexity just because it already exists. Do not introduce machinery because it looks architecturally impressive. Understand the real constraint, then fight for the smallest model that makes the correct behavior unsurprising.

Channel both "measure twice, cut once" and "yagni". Fight scope creep. Try to honor the dev's intent in both a minimal and realistic fashion.

When the documentation and existing code conflict, follow the code's conventions, and in your final reply, list all the inconsistencies you found.

Always build code cleanly, modularly, and with proper architecture and separation of concerns. Never simply bolt on a feature because I asked for it. If you can't find an easy way to build it naturally, and you'd recommend a refactor to make the whole thing cleanly, stop your work ahead of time and say so.

## Start a ticket

Work starts from a ticket: one issue that one PR closes. [The main flow](docs/agents/flow.md) says where tickets come from and who runs each step.

1. Run `git fetch origin main` and `git rev-list --left-right --count HEAD...origin/main`. A second count above 0 means this checkout, and this file, are behind `main`: update the base as [the development page](docs/development.md#start-a-task) describes before you edit or rely on them.
2. A skill these documents name is at `.agents/skills/<name>/SKILL.md`: read that copy and follow it.
3. One ticket is one thread, one worktree, one branch and one small PR. An idea with no ticket gets one before it is built: file it with the ticket form in `.github/ISSUE_TEMPLATE/agent_task.yml` and its `area:*` label when it fits one PR, and take it to the owner for `/grill-with-docs` when it needs more.
4. Many agents work here at once: check `gh pr list --json number,headRefName,files`, and keep your files apart from other open PRs outside your own stack.

## Stack

Scripts are PowerShell 7; `scripts/install.ps1` and `scripts/uninstall.ps1` also run on Windows PowerShell 5.1. [Project structure](docs/development.md#project-structure) maps the folders.

## Before you push

- Build the ticket with `tdd`. A test you write, change or review is held to [Where a test belongs](CONTRIBUTING.md#where-a-test-belongs) and [Write tests that don't flake](CONTRIBUTING.md#write-tests-that-dont-flake).
- Run the [checks for the change](docs/development.md#checks-by-change). The full check runs only the tests that open no windows, so it runs alongside other agents; CI runs the windowed ones, and [A windowed test](docs/development.md#a-windowed-test) says how to run one on purpose.
- Before opening the PR, review the diff with `code-review`, from `origin/main` or the branch below yours in a stack. Fix what it finds and run it again, until it finds nothing.
- Open the PR as [Open a PR](CONTRIBUTING.md#open-a-pr) describes, and own it until `gh pr view <number> --json state --jq .state` prints `MERGED`. A delegate hands it to its lead instead.
- A delegate ends its turn only when every command and every subagent it started has finished: each turn it ends wakes its lead. Start subagents in the foreground. Run commands in the foreground too, or wait for a background one before the turn ends (in T3 Code, with `t3_background_wait`). The full check takes about five minutes, so it fits in the foreground.

## When you

- Resolve a conflict, merge, or change a shared file (a build file, `tests/TestSupport/`, a lockfile, a type more than one module uses): [Conflicts and merging](CONTRIBUTING.md#conflicts-and-merging).
- Build related work as a stack: [Pull request stacks](docs/development.md#pull-request-stacks).
- Lead a spec's tickets, or work as a delegate: [the lead page](docs/agents/lead.md).
- Change something visible: [See what you changed](docs/development.md#see-what-you-changed).

## Models and effort

Take the model and effort for a subagent or a delegate from this table. Each job lists its models in order: use the first, and use the next only when the one before it is unavailable or its usage is used up.

| Job | First | Then | Then | Then |
| --- | --- | --- | --- | --- |
| Most work: leading a spec, building a ticket, the `code-review` subagents | Claude Opus 5.5 (medium) | GPT 6.1 Sol (medium) | Grok 4.7 Fast (high) | |
| A well-scoped everyday ticket: a bug fix, a docs change, a feature built against a clear spec | Claude Sonnet 5.5 (high) | GPT 6.1 Terra (high) | Grok 4.7 Fast (high) | |
| A side task for a larger model: a search, a lookup, classifying or extracting | Claude Haiku 5.5 (high) | GPT 6.1 Terra (medium) | Grok 4.7 Fast (high) | |
| A hard ticket: a migration, a performance fix, a change across several layers | Claude Opus 5.5 (high) | GPT 6.1 Sol (xhigh) | Grok 4.7 Fast (high) | |
| The hardest work, or a long unattended run | Claude Opus 5.5 (high) | GPT 6.1 Sol (xhigh) | Grok 4.7 Fast (high) | |

- A clear spec alone does not make a ticket a Sonnet ticket: a migration can be fully specified and still need Opus.
- The lead picks the row when it delegates. A ticket carries no model label.
- An agent that moves past the first model says in its report which model ran, and why the ones before it did not.

## Agent skills

Issues live in GitHub Issues for enpaze/winshell, used through `gh`: see the [issue tracker](docs/agents/issue-tracker.md) and [triage labels](docs/agents/triage-labels.md). Name things with the words of `GLOSSARY.md`: see [domain docs](docs/agents/domain.md).
