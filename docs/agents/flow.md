# The main flow

Every piece of work here runs through one flow of skills, in this order:

1. `/grill-with-docs`: the owner is interviewed until the design is settled.
2. `/to-spec`: the design becomes a spec.
3. `/to-tickets`: the spec becomes tickets.
4. `implement`: a ticket is built, with `tdd`.
5. `code-review`: the diff is reviewed against the ticket and the [standards](../../CONTRIBUTING.md#where-the-standards-live), until the review finds nothing. It is one review: the one `implement` runs before it commits is the one [AGENTS.md](../../AGENTS.md#before-you-push) asks for before the PR.
6. The PR and its merge.
7. `/retro`: what went wrong becomes better documents and checks.

A **spec** is the issue `/to-spec` writes. A **ticket** is one issue that one PR closes.

Work that needs more than one PR goes through all of it. Work that fits one PR needs one ticket and starts at `implement`. Nothing is built without a ticket.

## Who runs each step

- `/grill-with-docs`, `/to-spec`, `/to-tickets` and `/retro` run in a thread the owner is in. They need the owner's answers.
- A delegate follows `implement` and `code-review` for one ticket, then opens its PR and hands it to its lead. It ends its turn only when everything it started has finished, as [AGENTS.md](../../AGENTS.md#before-you-push) says. A ticket that is unclear or wrong stops it: it reports what it found instead of planning the work again.
- The lead reviews and merges the PR, as [the lead page](lead.md) describes. A thread with no lead sees its own PR to `MERGED`, as [Until it merges](../../CONTRIBUTING.md#until-it-merges) describes.

## Where this repo overrides a skill

- **One PR per ticket, stacked when tickets depend on each other.** Every ticket is reviewed and merged on its own, not landed with the rest of its spec on one integration branch.
- **A ticket ends at a merged PR.** `implement` ends at a commit; here the ticket closes when its PR merges, opened as [Open a PR](../../CONTRIBUTING.md#open-a-pr) describes.
- **`tdd` takes its seams from the spec and CONTRIBUTING.md when no user is there to confirm them.** The owner agreed the seams in the spec's Testing Decisions, and [Where a test belongs](../../CONTRIBUTING.md#where-a-test-belongs) names the owner of each contract.
- **`/retro` runs when a spec's last ticket merges, and after a one-off ticket only when something went wrong. Each finding the owner accepts becomes a ticket.** A finding left in a thread is forgotten.
- **The repo's pinned skills are the copies in use**: the ones `pwsh scripts/install-skills.ps1` installs from `skills-lock.json`. Another version of a skill names its files differently, so the documents here would point at files it lacks.
