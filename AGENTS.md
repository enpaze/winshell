# Winshell

## How we build

I like ambitious ideas, simple systems, and software that feels obvious. Do not preserve complexity just because it already exists. Do not introduce machinery because it looks architecturally impressive. Understand the real constraint, then fight for the smallest model that makes the correct behavior unsurprising.

Channel both "measure twice, cut once" and "yagni". Fight scope creep. Try to honor the dev's intent in both a minimal and realistic fashion.

When the documentation and existing code conflict, follow the code's conventions, and in your final reply, list all the inconsistencies you found.

Always build code cleanly, modularly, and with proper architecture and separation of concerns. Never simply bolt on a feature because I asked for it. If you can't find an easy way to build it naturally, and you'd recommend a refactor to make the whole thing cleanly, stop your work ahead of time and say so.

## Stack

Scripts are PowerShell 7; `scripts/install.ps1` and `scripts/uninstall.ps1` also run on Windows PowerShell 5.1. [Project structure](docs/development.md#project-structure) maps the folders.

## Models and effort

Take the model and effort for a subagent or a delegate from this table. Each job lists its models in order: use the first, and use the next only when the one before it is unavailable or its usage is used up.

| Job | First | Then | Then | Then |
| --- | --- | --- | --- | --- |
| Most work: leading a spec or building a ticket | Claude Opus 5.5 (medium) | GPT 6.1 Sol (medium) | Grok 4.7 Fast (high) | |
| A well-scoped everyday ticket: a bug fix, a docs change, a feature built against a clear spec | Claude Sonnet 5.5 (high) | GPT 6.1 Terra (high) | Grok 4.7 Fast (high) | |
| A side task for a larger model: a search, a lookup, classifying or extracting | Claude Haiku 5.5 (high) | GPT 6.1 Terra (medium) | Grok 4.7 Fast (high) | |
| A hard ticket: a migration, a performance fix, a change across several layers | Claude Opus 5.5 (high) | GPT 6.1 Sol (xhigh) | Grok 4.7 Fast (high) | |
| The hardest work, or a long unattended run | Claude Opus 5.5 (high) | GPT 6.1 Sol (xhigh) | Grok 4.7 Fast (high) | |

- A clear spec alone does not make a ticket a Sonnet ticket: a migration can be fully specified and still need Opus.
- The lead picks the row when it delegates. A ticket carries no model label.
- An agent that moves past the first model says in its report which model ran, and why the ones before it did not.

## Domain language

Name domain concepts with the words of [GLOSSARY.md](GLOSSARY.md): see [domain docs](docs/agents/domain.md).
