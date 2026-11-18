# Media

`shell.Media` lists the media sessions that apps share with Windows: what is playing, in which app, with play, pause, skip and seek. A configuration uses it for media controls. The feature shell's row `service.media` shows `sessions: N`, the number of sessions in `Sessions`.

## Sub-features

- `sessions` `Sessions` lists each `MediaSession`, with its app, `PlaybackState` and track title, artist, album and art.
- `transport` `Play()`, `Pause()`, `TogglePlaying()`, `Stop()`, `Next()` and `Previous()` control a session when its `Can...` value allows it.
- `position` `Position`, `Length`, `Seek(offset)` and `RefreshPosition()` read and move the playback position.
- `modes` `Rate`, `Shuffle` and `LoopState` read and set the playback rate, shuffle and repeat.
- `switch-to` `SwitchTo()` brings the session's app forward.
- `track-changed` `TrackChanged` and `PostTrackChanged` say when the track changes.

## How to get to it (user POV)

- A configuration reads `shell.Media`, or `Shell.Media` in a widget.
- The starter and the omarchy-style example do not use it, so no control shows it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the session count.** Read the `service.media` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.media`, and its `text` matches `^sessions: [0-9]+$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.media", "has": {"text": "^sessions: [0-9]+$"}, "within": 20}
]
```

## Gotchas

- The count is the number of apps that share a session now. With nothing playing, or on a CI runner, it is `sessions: 0`, and the step accepts that.
- When Windows' media session service does not answer, no app can list sessions, and the log says `media: Windows' media session service (Now Playing Session Manager) is not answering`. The row shows `sessions: 0` until it answers.
- The transport methods control the user's real player. No step calls them.
