# CPU

`shell.Cpu` reports how busy the processor is: `Usage` is the share of time that all cores were busy, sampled every two seconds while something follows it. A shell uses it for a CPU meter. The feature shell's row `service.cpu` shows `cpu: N%`, rounded to a whole percent.

## Sub-features

- `usage` `Usage` is the share of time that all cores were busy between the last two samples.
- `sampling` the service samples every two seconds while a component or a handler follows its `PropertyChanged`, and stops when nothing follows it.

## How to get to it (user POV)

- A shell reads `shell.Cpu`, or `Shell.Cpu` in a widget, and follows it with `UseObservable` or `PropertyChanged`.
- The starter and the omarchy-style example do not use it, so no control shows it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the usage.** Read the `service.cpu` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.cpu`, and its `text` matches `^cpu: [0-9]{1,3}%$`, a whole percent. The driver retries for 20 seconds.

```json
[
  {"element": "service.cpu", "has": {"text": "^cpu: [0-9]{1,3}%$"}, "within": 20}
]
```

## Gotchas

- `Usage` is 0 until two samples exist, about two seconds after the row starts to follow it. `cpu: 0%` alone does not prove a sample, and the step accepts any whole percent.
- `Usage` goes back to 0 when the last listener stops following the service.
