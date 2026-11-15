# Domain Docs

## Before exploring

Read `GLOSSARY.md` at the repo root before exploring the codebase.
Read ADRs in `docs/adr/` that touch the area you're about to work in.

This repo uses a single-context layout: one root glossary and one ADR directory.
If a domain document is missing, proceed silently. Domain modeling creates
documents when terms or decisions get resolved.

## Use the glossary's vocabulary

When your output names a domain concept (in an issue title, a refactor
proposal, a hypothesis or a test name), use the term as defined in the glossary.

If the concept you need is missing, reconsider whether it belongs to the
project's vocabulary.

## Flag ADR conflicts

If your proposal contradicts an existing ADR, identify the ADR and explain
why the decision should be reconsidered.
