# Recent documents

`shell.RecentDocuments` searches the documents that the user opened recently, the shortcuts in the user's Windows Recent folder. A configuration uses it to show recent files in a launcher. The feature shell's row `service.recent-documents` shows `recent: N`, the number of documents that `Search("")` returns, which is always 0.

## Sub-features

- `search` `Search(query)` returns each recent document whose name contains every word of the query, without regard to case.
- `order` the results come newest first, at most 20 of them.
- `document` each `RecentDocument` has its `Name` and its `Path`, the path of the shortcut in the Recent folder.
- `empty-query` a query with no words returns no documents.

## How to get to it (user POV)

- A configuration calls `shell.RecentDocuments.Search(query)`, or `Props.Shell.RecentDocuments.Search(query)` in a widget.
- The starter and the omarchy-style example do not use it, so no control shows it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the search result.** Read the `service.recent-documents` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.recent-documents`, and its `text` matches `^recent: [0-9]+$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.recent-documents", "has": {"text": "^recent: [0-9]+$"}, "within": 20}
]
```

## Gotchas

- An empty query returns no documents, so the row shows `recent: 0` on every PC. The step proves that `Search` runs and the row renders, not that a recent document is found.
- The service has no change notice, so the row does not update by itself.
- The results depend on the user's history, and a new account or a CI runner has few or none.
