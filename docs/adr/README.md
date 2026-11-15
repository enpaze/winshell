# Decisions

One line per ADR. When a decision changes, rewrite its ADR and its line here.

- [0001](0001-shells-are-built-from-public-building-blocks.md): a shell is primitives, building blocks, and compositions, and a composition uses only public API.
- [0002](0002-one-preferences-file-per-shell.md): each shell keeps the user's choices in one TOML file.
- [0003](0003-widgets-are-plain-controls.md): superseded by [0005](0005-ui-is-built-with-reactor.md).
- [0004](0004-watch-tracks-what-a-block-reads.md): superseded by [0005](0005-ui-is-built-with-reactor.md).
- [0005](0005-ui-is-built-with-reactor.md): the UI in a window is Reactor components, one tree per window.
- [0006](0006-placement-lists-are-immutable-arrays.md): a shell's placement lists are `ImmutableArray<Placement>`.
- [0007](0007-one-shell-runs-per-windows-session.md): one shell runs in a Windows session.
- [0008](0008-a-shell-project-lives-anywhere-and-the-chosen-shell-is-a-copy.md): a shell project lives wherever its author keeps it, and the chosen shell is a copy.
- [0009](0009-winshell-and-winland-are-siblings.md): Winshell and Winland are siblings. Same shape and release mechanics, no shared package, and each product keeps its own install.
