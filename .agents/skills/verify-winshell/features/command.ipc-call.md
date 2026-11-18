# winshell ipc call

`winshell ipc call <target> <function> [args...]` calls a function that a running shell registered with `shell.Ipc.Handle`, and prints what it returns. The handler's own types say what it takes and returns: `string`, `int`, `bool`, `double`, `float` and `Windows.UI.Color` cross the pipe, and nothing is evaluated. On the `features` shell, the target `features` has the function `echo`, which returns its one argument as it is.

## Sub-features

- `call-print` prints what the function returns on one line. A `bool` prints as `true` or `false`, a color as `#aarrggbb`, and a function that returns nothing prints nothing.
- `call-args` converts each argument to the type of its parameter. Words after the function name are arguments, even when they start with `-`.
- `call-refuse` exits with 1 for a target or function the shell did not register (`Target not found: <target>.` or `Function not found: <target>.<function>.`), for the wrong number of arguments, and for an argument that is not of its parameter's type.
- `call-missing` says what is missing and exits with 2 when the target or the function is left out.
- `call-notice` `winshell ipc call notice dismiss` closes Winshell's own notice for a build error or a failure. It takes no arguments.

## How to get to it (user POV)

- Run `winshell ipc call <target> <function> [args...]` in a terminal, such as `winshell ipc call notes hello Ada`.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-c <name>` or `-p <path>` before `call`. With none, the command acts on the only running shell.
- A configuration registers a function with `shell.Ipc.Handle("<target>", "<function>", handler)`, and disposes the registration to take it away.
- `winshell ipc show` lists the functions that `ipc call` can reach.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.

- **Call echo.** Call the feature shell's `echo` function with one argument. Run `winshell ipc --pid {pid} call features echo feature-map`. It exits with 0 and prints `feature-map` on a line of its own, the argument it was given.

```json
[
  {"winshell": ["ipc", "--pid", "{pid}", "call", "features", "echo", "feature-map"], "output": "^feature-map$"}
]
```

## Gotchas

- Options after the function name go to the function, not to `winshell`: `winshell ipc call notes add -v` passes `-v` to `add`. Put `--pid` and the other instance options before `call`.
- Target and function names are case-sensitive. `Features` is not the target `features`.
- `notice dismiss` is not a target the configuration registered, so `winshell ipc show` does not list it.
- The CLI and the shell must come from the same release. Otherwise each refuses the other with `This shell is Winshell <shell> and this winshell is <client>.`, so build the shell with the `winshell` you drive it with.
- With several shells running and no instance option, the command exits with 2 and lists them.
