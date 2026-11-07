# Input injection and MP4 recording for `winshell inspect`

Research for [#1718](https://github.com/enpaze/winshell/issues/1718), the first layer of [#1717](https://github.com/enpaze/winshell/issues/1717). It answers what `inspect record` ([#1719](https://github.com/enpaze/winshell/issues/1719)) and the real input commands ([#1720](https://github.com/enpaze/winshell/issues/1720)) can build on. The decisions I1–I8 in #1717 stand. Where the findings bear on one of them, the [last section](#what-blocks-a-decision-in-1717) says so.

The facts come from Microsoft Learn, Raymond Chen's Microsoft blog, the Windows SDK 10.0.26100.0 headers, and [microsoft/microsoft-ui-xaml](https://github.com/microsoft/microsoft-ui-xaml) at commit `78c72df`. The results come from two throwaway probes, which are not committed. Both ran on Windows 11 Pro 10.0.26200, on one 2560×1600 screen at 150% scale, with Windows App SDK 2.6.4.

- **Input probe.** A WinUI 3 window with two `Button`s, and a third in the last Shift+Tab run. It logged `PointerEntered`, `PointerExited`, `PointerPressed`, `PointerReleased`, `PointerWheelChanged`, `KeyDown`, `GotFocus` with its `FocusState`, `Click`, `RightTapped`, and each `CommonStates` visual state.
  - **Steps.** Each candidate ran the same steps: move into the window, move onto a button, press, right press, wheel, leave, Tab, Space.
  - **Two runs each.** Posted messages, `SendInput` and `InputInjector` each ran once from a second process and once from a background thread inside the window's process. The pen ran from a second process only. The in-process run that posted to the top-level window came from an earlier build of the probe, with no Space step.
  - **In front.** For those runs the probe took the foreground like the fallback in Winshell's focus takeover: `KeyInput.Tap`, then `SetForegroundWindow`.
  - **Inactive.** A final keyboard run gave the window `WS_EX_NOACTIVATE` and left it inactive. It was not topmost, but `WindowFromPoint` at the button found the probe, so nothing covered it there. That run grabbed it with Windows.Graphics.Capture (WGC).
- **Media probe.** It used CsWin32 with `allowMarshaling: false`, as `Winshell.Native` does, to drive the Media Foundation sink writer. It timed frames with a copy of the repo's WGC interop.

## Input

### Probe results

| | Posted window messages | `SendInput` | `InputInjector` |
| --- | --- | --- | --- |
| `PointerEntered`, `PointerExited`, `PointerOver` state | No | Yes | Yes |
| `PointerPressed`, `Click`, `Pressed` state | No | Yes | Yes |
| Right press, `RightTapped` | No | Yes | Yes |
| `PointerWheelChanged` (−120) | No | Yes | Yes |
| `KeyDown`, Tab moves focus | Yes | Yes | Yes |
| Focus in `FocusState.Keyboard` | Yes, and the focus rectangle shows | Yes; rectangle not checked | Yes; rectangle not checked |
| Space clicks the focused button | Only when posted to the input site | Yes | Yes |
| Shift+Tab moves focus back | No: the Shift state is ignored, and focus moves forward | Not tried | Not tried |
| Moves the user's cursor | No | Yes | Yes |
| From another process | Same as in-process, for the input site | Same as in-process | Same as in-process |
| Needs the window in front | No, for keys | Pointer: unobscured. Keys: foreground | Same as `SendInput` |

- **Posted messages** went to the top-level `WinUIDesktopWin32WindowClass` or to its `InputSiteWindowClass` child, the "input site".
  - **Mouse.** `WM_MOUSEMOVE`, `WM_LBUTTONDOWN`/`UP`, `WM_RBUTTONDOWN`/`UP`, `WM_MOUSEWHEEL` and `WM_MOUSELEAVE` raised nothing, to either window.
  - **Keys.** `WM_KEYDOWN`/`WM_KEYUP` for Tab raised `KeyDown` and moved focus in `FocusState.Keyboard`. Space posted to the input site clicked the focused button. Space posted to the top-level window did nothing.
  - **Inactive.** On the inactive `WS_EX_NOACTIVATE` window, Tab and Space to the input site worked the same. A WGC frame showed the focus rectangle around the button.
  - **Shift+Tab.** A posted Shift down, Tab, Shift up raised `KeyDown` for both keys, but focus moved forward.
- **`SendInput` and `InputInjector`** behaved the same, event for event, from either process. `InputInjector.TryCreate()` returned an injector in an unpackaged, non-elevated process with no capability declared, and its input reached another unpackaged process.
- **Behind another window.** In an early run the probe was behind the user's maximized window. The injected move, clicks and wheel went to that window instead, and the probe saw nothing. Real input goes to whatever window is under the point. That run's log was overwritten later.
- **Coordinates.** The probe first ran without a DPI manifest, so it was DPI-unaware. Its screen points were then logical, `SendInput` took them as physical, and the pointer missed the button. With a per-monitor v2 manifest the button's screen point was right: `TransformToVisual(null)` on the button, times `XamlRoot.RasterizationScale`, plus the client origin from `ClientToScreen`.
- **Bonus: a synthetic pen.** An `InputInjector` pen hovering in range, not in contact, raised `PointerEntered` and `PointerOver` with `PointerDeviceType.Pen`. Pen down and up raised `PointerPressed` and `Click`. It did not move the cursor. The hover ended by itself (`PointerExited`) within a second when no update followed. The probe tried no barrel-button press, and a pen has no wheel.

### Why: what the sources say

- **WinUI 3 takes the mouse as `WM_POINTER`.** `WindowsXamlManager` calls `EnableMouseInPointer(TRUE)` ([WindowsXamlManager_Partial.cpp](https://github.com/microsoft/microsoft-ui-xaml/blob/78c72df1a5e6df181ce2d86a8a8cdff53be5ef22/dxaml/xcp/dxaml/lib/WindowsXamlManager_Partial.cpp#L288-L292)). [EnableMouseInPointer](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enablemouseinpointer) makes the mouse *"send WM_POINTER messages"*. A posted legacy mouse message is not in that pipeline, which matches the probe.
- **Keys arrive on the input site.** Microsoft's [desktop app walkthrough](https://github.com/microsoft/microsoft-ui-xaml/blob/78c72df1a5e6df181ce2d86a8a8cdff53be5ef22/docs/design-notes/desktop-app-walkthrough.md) shows the HWND tree. It says `InputSiteWindowClass` *"is an invisible 0x0 HWND … IXP code uses it to handle focus and keyboard input."* [focus.md](https://github.com/microsoft/microsoft-ui-xaml/blob/78c72df1a5e6df181ce2d86a8a8cdff53be5ef22/docs/design-notes/focus.md) traces keyboard messages from that window into the XAML island.
- **Posted keys skip the key state.** A posted message *"doesn't update the keyboard shift states"*, so `GetKeyState` sees the real modifiers ([Raymond Chen](https://devblogs.microsoft.com/oldnewthing/20050530-11/?p=35513)). The Shift+Tab run shows exactly that.
- **Real keys go to the foreground.** *"The system posts keyboard messages to the message queue of the foreground thread that created the window with the keyboard focus"* ([About Keyboard Input](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-keyboard-input)). The probe never sent `SendInput` keys while it was in the background, because they would have reached the user's window.
- **`SendInput` feeds the real input stream.**
  - Its events go *"into the keyboard or mouse input stream"* ([SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)).
  - It is limited by User Interface Privilege Isolation (UIPI): *"Applications are permitted to inject input only into applications that are at an equal or lesser integrity level"*, and a block is silent (same page).
  - Absolute moves take coordinates from 0 to 65535. `MOUSEEVENTF_VIRTUALDESK` maps them to the whole virtual desktop. Relative moves are accelerated. One wheel notch is `WHEEL_DELTA`, 120 ([MOUSEINPUT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-mouseinput)).
- **`InputInjector` is documented as needing a restricted capability.**
  - *"The APIs in this namespace require the inputInjectionBrokered restricted capability"* ([InputInjector](https://learn.microsoft.com/en-us/uwp/api/windows.ui.input.preview.injection.inputinjector)).
  - Capabilities apply mainly to packaged AppContainer apps ([App capability declarations](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations)). That it works unpackaged is observed, not documented.
  - [InjectedInputMouseOptions](https://learn.microsoft.com/en-us/uwp/api/windows.ui.input.preview.injection.injectedinputmouseoptions) mirrors the `MOUSEEVENTF_*` values, so it feeds the same mouse stream as `SendInput`.
  - Users report that a second `TryCreate()` crashes or injects nothing ([microsoft-ui-xaml#8639](https://github.com/microsoft/microsoft-ui-xaml/issues/8639)). Microsoft has not answered.
- **`PointerOver` comes from the control's pointer handling.** The built-in control logic *"will load that visual state whenever a pointer is over the control"* ([UIElement.PointerEntered](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.pointerentered)).
- **Focus rectangles need keyboard focus.** The focus manager draws one only for an element in `FocusState.Keyboard` with `UseSystemFocusVisuals` ([focusmgr.cpp](https://github.com/microsoft/microsoft-ui-xaml/blob/78c72df1a5e6df181ce2d86a8a8cdff53be5ef22/dxaml/xcp/core/dll/focusmgr.cpp#L2712-L2758)). `Focus(FocusState.Programmatic)` becomes keyboard focus only when the last input was a keyboard or a gamepad ([CoerceFocusState](https://github.com/microsoft/microsoft-ui-xaml/blob/78c72df1a5e6df181ce2d86a8a8cdff53be5ef22/dxaml/xcp/core/dll/focusmgr.cpp#L3200-L3240), [FocusState](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.focusstate)).
- **Synthetic pens.** [CreateSyntheticPointerDevice](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-createsyntheticpointerdevice) takes only `PT_TOUCH` or `PT_PEN`, and `InputInjector` has its own pen injection. No page says whether pen hover moves the mouse cursor; the probe says it doesn't.

### What it means for Winshell

- **Pointer input** (`point`, `press`, `press --right`, `scroll`) needs `SendInput` or `InputInjector`. Both move the user's cursor. I5's "the cursor goes back" therefore means saving the cursor position and restoring it with `Cursor.MoveTo` (`SetCursorPos`) afterwards. The restore should end the hover with a `PointerExited`, but the probe did not check that.
- **A real press activates the window under it**, unless the window has `WS_EX_NOACTIVATE` or answers [WM_MOUSEACTIVATE](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-mouseactivate) with `MA_NOACTIVATE`. Winshell decides that per window, in `HostFocusEffects` and `HostMessages`.
- **Keyboard input** (`key`, and Tab-driven `focus`) works by posting `WM_KEYDOWN`/`WM_KEYUP` to the window's input site.
  - It works for plain keys, leaves the cursor and the foreground alone, and reaches an inactive `WS_EX_NOACTIVATE` window.
  - The Shift state was ignored. By the same cause, other modifiers should be too. Setting the key state from inside the shell (`SetKeyboardState`) before posting was not tried.
  - Real keys from `SendInput` go to the foreground window. A Winshell panel can have `WS_EX_NOACTIVATE` (`HostFocusEffects`) and never be in front, so its keys would reach the user's app instead.
- **Where to inject.** The probe got the same result from inside the process and from another one, so either works. Inside the shell is simpler:
  - The shell already has the element's place and its window.
  - It shares the shell's integrity level, so UIPI never blocks it.
  - The CLI already reaches it over IPC.
  - The probe injected from a background thread. The UI thread must not wait for its own injected input, which it has to pump to handle.
- **The visible check** (I5) is `WindowFromPoint` at the injection point, rooted with `GetAncestor(GA_ROOT)`, before every pointer event: it must be the shell's own window. The probe ran this check before each step and stopped any run whose point belonged to another window.
- **DPI.** Screen points must be physical, and the probe got them right only when per-monitor v2 aware.
  - The `winshell` template has no `app.manifest`, and Winshell never calls `SetProcessDpiAwarenessContext`, though code such as `WindowBackend.MoveToMonitor` already assumes per-monitor awareness.
  - The CLI's `winshell doctor` checks its own process. Only `Doctor.ForThisProcess`, run inside a shell, reports the shell's awareness.
  - #1720 must confirm a configuration built from the template is per-monitor v2 aware.
- **Interop.** No new package and no exemption from `BannedSymbols.Interop.txt` is needed.
  - `SendInput`, `SetCursorPos`, `PostMessageW`, `WindowFromPoint`, `ClientToScreen`, `GetCursorPos`, `GetAncestor` and `EnumChildWindows` are already in `Winshell.Native`.
  - `MapVirtualKey`, for a key's scan code, is the one new declaration.
  - `InputInjector` is a WinRT projection and needs no declaration at all.

### Recommendation

- **Pointer: `SendInput`.** Use absolute virtual-desktop coordinates, run it inside the shell behind the `WindowFromPoint` check, then restore the cursor. Prefer it to `InputInjector`. The result is the same, `SendInput` is already declared, and its unpackaged use is documented, while `InputInjector`'s is not and has open bug reports.
- **Keyboard: posted messages.** Post `WM_KEYDOWN`/`WM_KEYUP` to the window's input site for plain keys. For a chord with a modifier, use `SendInput`, and only while the shell's window is in front. Otherwise refuse with a message that says why. That `SendInput` path is untested, so #1720 probes it first.
- **`focus <name>`: Tab until it lands.** Post Tab until `FocusManager.GetFocusedElement` is the named element, with a limit. That is real input, aimed by name, and the probe saw it draw the focus rectangle in an inactive window.
- **Not the pen.** It moves no cursor, but it reports `PointerDeviceType.Pen`, has no wheel, and its hover expires without updates.

## Recording

### Probe results

- **Frames seem to follow visible change.** These counts came from console output. The cursor was left out of the capture.
  - A static WinUI window in the background gave 1 frame in 2 s.
  - The whole screen, with ordinary activity, gave 6 frames in 2 s, 80 to 558 ms apart.
- **Timestamps share a clock with the shell.** The first frame's `SystemRelativeTime` came 5–7 ms after `Stopwatch.GetTimestamp()` read at start, so they are on one timeline.
- **WGC captures an inactive window.** The frame of the inactive probe showed its content and its focus rectangle.
- **The sink writer encodes BGRA to H.264.** It accepted `MFVideoFormat_RGB32` input for an `MFVideoFormat_H264` stream, with hardware transforms allowed and throttling off. With `MF_MT_DEFAULT_STRIDE` set to `width × 4`, top-down rows like WGC's came out the right way up.
- **The output is constant rate.** Each test used sample time `k × 10⁷/fps` and duration `10⁷/fps`, and every file was Constrained Baseline.
  - 90 frames at 1280×720 and 30 fps: ffprobe reported `r_frame_rate=30/1`, `avg_frame_rate=30/1`, 90 frames and 3.000 s, with frames 33.3 ms apart.
  - 240 frames at 2560×1600 and 60 fps gave `60/1` and 4.0 s.
- **Sizes and speed.**
  - An odd size, 1281×721, was accepted and kept.
  - 240 synthetic 2560×1600 frames encoded in 2.6 s, about 91 fps, from CPU memory buffers, including the time to fill them. The frames were flat colors with a moving bar, which compress easily.
  - No capture ran during the encode, and the probe didn't check whether a hardware encoder was used.

### Why: what the sources say

- **Capture.**
  - `FrameArrived` fires *"when a captured frame is stored in the frame pool"* ([FrameArrived](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.direct3d11captureframepool.framearrived)).
  - A free-threaded pool raises it on *"the frame pool's internal worker thread"* ([CreateFreeThreaded](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.direct3d11captureframepool.createfreethreaded)).
  - [SystemRelativeTime](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.direct3d11captureframe.systemrelativetime) is *"the QPC (Query Performance Counter) time at which the compositor rendered the frame."*
  - No page says when frames are produced.
- **Throttling.** `GraphicsCaptureSession.MinUpdateInterval` is in `IGraphicsCaptureSession5`, UniversalApiContract 19.0, which is Windows 11 24H2 (SDK `windows.graphics.capture.idl`). Its [Learn page](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.graphicscapturesession.minupdateinterval) has no description. It is not in the 10.0.22621 projection Winshell targets.
- **Frames that are held.** Disposing a frame returns its buffer to the pool ([Screen capture](https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture)). No page says what happens to new frames while every buffer is held.
- **The H.264 encoder takes only YUV.**
  - Its inputs are *"I420, IYUV, NV12, YUY2, YV12"* ([H.264 Video Encoder](https://learn.microsoft.com/en-us/windows/win32/medfound/h-264-video-encoder)).
  - The sink writer converts: *"By default, the source reader and sink writer can perform some format conversions"* ([MF_READWRITE_DISABLE_CONVERTERS](https://learn.microsoft.com/en-us/windows/win32/medfound/mf-readwrite-disable-converters)).
  - It doesn't change the rate: *"The sink writer does not support video resizing, frame-rate conversion, or audio resampling, unless these functions are provided by the encoder"* ([Using the Sink Writer](https://learn.microsoft.com/en-us/windows/win32/medfound/using-the-sink-writer)).
  - That RGB32 converts to H.264 is observed, not documented.
- **The sink writer API.**
  - [MFCreateSinkWriterFromURL](https://learn.microsoft.com/en-us/windows/win32/api/mfreadwrite/nf-mfreadwrite-mfcreatesinkwriterfromurl) picks the container from the `.mp4` extension and needs [MFStartup](https://learn.microsoft.com/en-us/windows/win32/api/mfapi/nf-mfapi-mfstartup) first.
  - [MFShutdown](https://learn.microsoft.com/en-us/windows/win32/api/mfapi/nf-mfapi-mfshutdown) runs once per startup, never from a work queue thread.
  - Sample [time](https://learn.microsoft.com/en-us/windows/win32/api/mfobjects/nf-mfobjects-imfsample-setsampletime) and [duration](https://learn.microsoft.com/en-us/windows/win32/api/mfobjects/nf-mfobjects-imfsample-setsampleduration) are in 100 ns units.
  - `WriteSample` blocks to limit the data rate unless [MF_SINK_WRITER_DISABLE_THROTTLING](https://learn.microsoft.com/en-us/windows/win32/medfound/mf-sink-writer-disable-throttling) is set.
  - Hardware encoders are off unless [MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS](https://learn.microsoft.com/en-us/windows/win32/medfound/mf-readwrite-enable-hardware-transforms) is set.
  - A GPU path exists through [MF_SINK_WRITER_D3D_MANAGER](https://learn.microsoft.com/en-us/windows/win32/medfound/mf-sink-writer-d3d-manager) and [MFCreateDXGISurfaceBuffer](https://learn.microsoft.com/en-us/windows/win32/api/mfapi/nf-mfapi-mfcreatedxgisurfacebuffer).
- **CsWin32 covers it.**
  - CsWin32 0.3.367 generated `IMFSinkWriter`, `IMFMediaType`, `IMFAttributes`, `IMFSample`, `IMFMediaBuffer`, the `MFCreate*` functions, `MF_VERSION`, and the `MF_MT_*` and `MFVideoFormat_*` GUIDs from a `NativeMethods.txt` list.
  - With `allowMarshaling: false` the interfaces are structs of function pointers, with no `ComImport`. Their methods return nothing, and a failed call surfaced as an exception.
  - `MFSetAttributeSize` and `MFSetAttributeRatio` are inline header helpers, not exports. The probe packed the two values into one `UINT64` for `SetUINT64` itself.
- **A WinRT alternative.** Microsoft's [screen capture to video](https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture-video) sample feeds WGC surfaces through a `MediaStreamSource` into `MediaTranscoder`, with [MediaEncodingProfile.CreateMp4](https://learn.microsoft.com/en-us/uwp/api/windows.media.mediaproperties.mediaencodingprofile.createmp4). It needs no declarations. But it is a pull model, answering `SampleRequested`, and the sample does no constant-rate pacing. The probe did not try it.

### What it means for Winshell

- **"Every frame Windows presents" is every frame WGC delivers**, which seems to be every visible change. A stall or a one-frame flicker shows in the kept frames' timestamps, as I2 wants. Without `--fps`, the MP4's constant rate is the target screen's `ScreenInfo.RefreshRate`, and keeping every frame should stay at or under it.
- **The current `CaptureSession` can't record.** It samples the latest frame on a 16–1000 ms timer, and its readback can finish a tick late, so it drops frames by design. That is right for previews.
  - Recording needs `FrameArrived` on the free-threaded pool's thread.
  - That thread copies each kept frame out of the pool at once and disposes it, so held buffers never stall capture.
  - The copy uses the D3D immediate context, which is not thread-safe. The recording needs its own device and context, or `ID3D11Multithread` protection.
- **The cursor.** `CaptureSession.IncludeCursor` exists and is off by default. For I7's "point, then look" recipe, a recording would usually want it on. Cursor movement then adds frames. The system's capture border stays: the backend never asks for borderless capture.
- **Capping to `--fps`** means dropping by timestamp: keep a frame when its `SystemRelativeTime` reaches the next slot, then advance the slot by `1/N`. `MinUpdateInterval` would need a 26100 target framework, a runtime check, and semantics no page states.
- **Constant-rate MP4.** Output frame *k* gets time `k × 10⁷/fps` and duration `10⁷/fps`. It shows the newest kept frame at or before that slot. A slot with no new frame repeats the last one. Extra frames in a slot are dropped from the MP4 but stay in `--frames`.
- **Interop and banned symbols.**
  - The Media Foundation declarations go in `Winshell.Native` through CsWin32, like the capture interop. Nothing touches `BannedSymbols.Interop.txt` or `System.Runtime.InteropServices.ComTypes`.
  - The clock ban (`Stopwatch`, `DateTime.Now`) is no obstacle. Frame times come from `SystemRelativeTime`. `--for` and any other timing use `TimeProvider`. Its default `GetTimestamp` *"returns Stopwatch.GetTimestamp()"* ([TimeProvider.GetTimestamp](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider.gettimestamp)), the clock the probe found aligned with `SystemRelativeTime`.

### Recommendation

- **Capture: a recording path beside the preview.** Build it beside `CaptureSession`'s preview path, from the same `WindowsCaptureBackend` pieces, driven by `FrameArrived`. Use `IGraphicsCaptureItemInterop.CreateForWindow` for a window and `CreateForMonitor` for `--screen`; both are already declared.
- **Cap: drop by `SystemRelativeTime`.** Leave `MinUpdateInterval` out.
- **Encode: the Media Foundation sink writer, from `Winshell.Native`.**
  - Use top-down RGB32 input with `MF_MT_DEFAULT_STRIDE`, and H.264 into `.mp4`.
  - Allow hardware transforms, turn throttling off, and run it on a thread that isn't the UI thread.
  - Pace it to a constant rate by repeating and dropping frames.
  - Feed CPU buffers first, since `--frames` needs the CPU pixels anyway. Measure real screen content at the target sizes before moving to DXGI surface buffers.

## What blocks a decision in #1717

- **The docs forbid real input for verification.** The Migration and delivery section of `docs/architecture.md` says: *"Never inject real user input for verification."* I5 and I7's recipe (`point`, then read the contact sheet) do exactly that. `docs/development.md` also says *"Tests do not inject keystrokes"*. #1720 has to change both lines before the recipe lands, and before a slow test injects input.
- **`point` and `leave` against "the cursor goes back".** If the cursor goes back as soon as `point` returns, the hover ends with it, and `leave` has nothing left to do. One reading keeps both: the cursor stays on the element from `point` until `leave`, or until the next pointer command, which restores it. #1720 needs that settled.
- **Keyboard without the foreground can't take modifiers.** For non-activating panels, `key` and `focus` go through posted key messages, which raise the routed events and draw the focus rectangle. Shift+Tab doesn't work that way, and other chords such as Ctrl+S very likely don't either. I6's `key <keys>` either refuses such chords on a window that isn't in front, or limits itself to plain keys.
- **"Refuses a target that isn't visible"** can only be checked per point, with `WindowFromPoint`. A window can be on screen while another window, such as a toast or a popup, covers the one element.

## Not verified

- That restoring the cursor with `SetCursorPos` raises `PointerExited`. The probe skipped that run once the user was working at the machine.
- Modifier chords through `SendInput` or `InputInjector`.
- Pointer input to a window that is visible but not in front. Every pointer run that worked had the probe in front.
- Injection across integrity levels, such as into an elevated shell.
- `Focus(FocusState.Keyboard)` drawing the focus rectangle. The source says it does; the probe did not call it.
- That a configuration built from the template runs per-monitor v2 aware.
- That WGC captures a covered window's own pixels, rather than the windows over it.
- What WGC does with new frames while every pool buffer is held.
- Encoding real screen content, sizes above 2560×1600, more than one screen, and machines without a hardware H.264 encoder.
- The `MediaTranscoder` path.
