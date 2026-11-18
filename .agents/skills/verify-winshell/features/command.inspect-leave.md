# winshell inspect leave

`winshell inspect leave` ends a `winshell inspect point`. It puts the cursor back where it was before `point`, by way of a spot in the same window off the element, so the element sees the pointer leave and `PointerExited` runs, and it prints `The cursor is back where it was.` With nothing pointed at, it says so and moves nothing. The driver proves that case on a freshly started shell.

## Sub-features

- `leave-back` puts the cursor back where it was before `point`, so `PointerExited` runs.
- `leave-hover-kept` says when an element that fills its window may keep its hover look until the mouse moves.
- `leave-moved` leaves the cursor where it is when the user moved the mouse since `point`, and says so.
- `leave-nothing` says `Nothing is pointed at: there was no point, or it has ended.` and moves nothing when there is no `point` to end.

## How to get to it (user POV)

- Run `winshell inspect leave` in a terminal after `winshell inspect point <name>`.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-c <name>` or `-p <path>`. With none, the command acts on the only running shell.
- `focus`, `point`, `press`, `scroll` and `key` also end a `point` first, and so do 30 seconds and a move of the mouse.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- No `winshell inspect point` has run against this shell.

- **Leave with nothing pointed at.** End a point that never started. Run `winshell inspect --pid {pid} leave`. It exits with 0, and its output starts with `Nothing is pointed at`.

```json
[
  {"winshell": ["inspect", "--pid", "{pid}", "leave"], "output": "^Nothing is pointed at"}
]
```

## Gotchas

- This feature does not prove that the cursor goes back. That needs a real `point` first, which moves the user's cursor, and the driver sends no real input. Do not report `leave-back` as verified.
- The step needs a shell that no `point` has touched in the last 30 seconds. A `point` that is still held makes `leave` move the real cursor.
- `leave` is the one real input command that does not refuse while someone uses the PC. It still refuses while another real input command runs.
- When the element fills its window, it may keep its hover look until the mouse moves, and `leave` says so.
