# CaptureView

A `CaptureView` is a control that shows the frames of a `CaptureSession`: a live picture of a window or a screen, scaled to fit the space it has. In the `features` shell, a 64 by 48 view captures the shell's own window, and its automation name is the session's state, such as `capture view: running, frame <width>x<height>`, or `capture view: stopped, no frame` with the session's error after it.

## Sub-features

- `show-frames` shows each new frame of the session it is given, and nothing when the session has no frame.
- `fit` keeps the frame's shape and fits it to the space the view has, at the window's scale.
- `target` captures one window with `CaptureTarget.ForWindow` or one screen with `CaptureTarget.ForScreen`.
- `session-state` reports `Running`, `HasFrame`, `PixelSize` and `Error` on the session.
- `frame-options` sets the session's `FrameInterval` and whether it includes the cursor with `IncludeCursor`.
- `save` saves the current frame with `SaveToFile` or copies it with `CopyToClipboard`.

## How to get to it (user POV)

- In a configuration, make a session with `new CaptureSession(shell.Loop, shell.Log)`, set its `Target`, and call `Start()`.
- Put `new CaptureView { Session = session }` in a window's content. In a Reactor component, embed it with `XamlHost`.
- In the `features` shell, the view is the first picture beside the rows, with the automation id `block.capture-view`.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's floating window is open, so the window list finds it and the session can start.

- **Read the capture.** Wait for the session to capture the feature window. Run `winshell inspect --pid {pid} tree`. Exactly one element has the automation id `block.capture-view`, and its `label` says the session is running with a frame of a size above zero, matching `^capture view: running, frame [1-9][0-9]*x[1-9][0-9]*$`. The driver retries for 20 seconds.

```json
[
  {"element": "block.capture-view", "has": {"label": "^capture view: running, frame [1-9][0-9]*x[1-9][0-9]*$"}, "within": 20}
]
```

## Gotchas

- The label comes from the session, not from the view. It proves that frames arrive, not that the view draws them. Take a screenshot of the window and look at the picture to prove that.
- The session starts only after the window list lists the shell's own window, so the first tree reads can say `stopped, no frame`. That is why the step retries for 20 seconds.
- A label that ends with an error names why the capture failed. Read it before you rerun.
- Closing the feature window hides it, and `tree` lists only open windows, so the step finds nothing. Start the shell again.
