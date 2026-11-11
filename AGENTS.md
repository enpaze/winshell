# Winshell

## How we build

I like ambitious ideas, simple systems, and software that feels obvious. Do not preserve complexity just because it already exists. Do not introduce machinery because it looks architecturally impressive. Understand the real constraint, then fight for the smallest model that makes the correct behavior unsurprising.

Channel both "measure twice, cut once" and "yagni". Fight scope creep. Try to honor the dev's intent in both a minimal and realistic fashion.

When the documentation and existing code conflict, follow the code's conventions, and in your final reply, list all the inconsistencies you found.

Always build code cleanly, modularly, and with proper architecture and separation of concerns. Never simply bolt on a feature because I asked for it. If you can't find an easy way to build it naturally, and you'd recommend a refactor to make the whole thing cleanly, stop your work ahead of time and say so.

### Roles and model choices

The coordinator delegates to explorers and builders on demand. The reviewer checks the resulting changes independently. These are agent roles: in the workflow docs, owner means User / Human, lead means Coordinator, and a delegate is a dispatched agent in its assigned role. Both `code-review` subagents use the Reviewer role.

| Role | Responsibilities and limits | Preferred | Fallback 1 | Fallback 2 |
| --- | --- | --- | --- | --- |
| User / Human | Decisions, text, go/no-go | — | — | — |
| Coordinator | Plan, brief, delegate on demand, land PRs, maintain docs | Claude Opus 5.5 (high) | GPT 6.1 Sol (xhigh) | Grok 4.7 Fast (high) |
| Explorer | Find files and report evidence; leave conclusions to the coordinator | Claude Haiku 5.5 (xhigh); Claude Sonnet 5.5 (xhigh) when judgment is needed | GPT 6.1 Terra (medium) | Grok 4.7 Fast (high) |
| Builder | Write code and tests in its own worktree | Claude Opus 5.5 (high) | GPT 6.1 Sol (xhigh) | Grok 4.7 Fast (high) |
| Reviewer | Independently check changes and report findings; read-only: no edits, commits, pushes or merges | Claude Opus 5.5 (high) | GPT 6.1 Sol (xhigh) | Grok 4.7 Fast (high) |

- Before an agent role is first needed in a thread, the coordinator asks the user to choose its model through T3 Code's structured question tool. Offer all model/effort pairs in that role's row as options, recommend the preferred choice appropriate to the work, and wait for an answer before starting the role's work.
- Reuse the user's selection for that role throughout the thread. If the selected model is unavailable, its usage is exhausted, or the effort setting is unsupported, explain and ask again with the remaining table choices. Wait for the user's answer; never switch silently.
- Every subagent or delegate prompt must explicitly name its assigned role and include that role's responsibilities and limits from the table. Before dispatching work that will need further agent roles, the coordinator collects those model choices too and includes them in the brief. Each dispatcher sets the selected model and effort explicitly; children reuse the supplied assignments and role selections.

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

## Agent skills

Issues live in GitHub Issues for enpaze/winshell, used through `gh`: see the [issue tracker](docs/agents/issue-tracker.md) and [triage labels](docs/agents/triage-labels.md). Name things with the words of `GLOSSARY.md`: see [domain docs](docs/agents/domain.md).
