# Winshell

## Stack

Scripts are PowerShell 7; `scripts/install.ps1` and `scripts/uninstall.ps1` also run on Windows PowerShell 5.1. [Project structure](docs/development.md#project-structure) maps the folders.

## Agent skills

### Issue tracker

Issues and specs live in GitHub Issues for `enpaze/winshell`. See `docs/agents/issue-tracker.md`.

### Triage labels

Use the five default triage labels. See `docs/agents/triage-labels.md`.

### Verification

To see a change working in a real shell, use the `verify-winshell` skill in `.agents/skills/verify-winshell/`. It drives shells with `winshell inspect`, and its feature map lists every feature with the steps that prove it.

### Domain docs

The glossary is `GLOSSARY.md`. Before exploring code or naming domain concepts, read `docs/agents/domain.md`.
