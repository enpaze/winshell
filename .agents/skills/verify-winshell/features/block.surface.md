# Surface

A `Surface` is a component that paints the style's background behind its content, in the window's light or dark theme, and follows a theme switch while the shell runs. Every bar's areas sit in one. In the `features` shell, a surface with a padding of 4 holds the text `surface: painted` and the feature widget area.

## Sub-features

- `background` paints the style's `ShellBackgroundBrush` behind its content.
- `theme` follows the window's light or dark theme, and a switch while the shell runs.
- `padding` keeps the `Thickness` it is given between its edge and its content.
- `style` takes its own `Style` over the shell's, and restyles in place when `shell.Style` changes, so what it holds stays loaded.
- `content` holds any Reactor element, such as a `WidgetArea`.

## How to get to it (user POV)

- Show a component in a `PanelWindow<T>`, `PopupWindow<T>` or `FloatingWindow<T>`, which puts it on a surface.
- In a Reactor component, return `Surface(content, new Thickness(4))` for a part in a style of its own.
- Every `Bar` shows its three areas in a surface, so `winshell inspect tree` names a bar window's content type `Surface`.
- In the `features` shell, the surface is the box under the `Click` and `Popup` buttons.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.

- **Read the surface's content.** Start the feature shell and look under the buttons. Run `winshell inspect --pid {pid} tree`. Exactly one element has the automation id `block.surface`, and its `text` is `surface: painted`, matching `^surface: painted$`.

```json
[
  {"element": "block.surface", "has": {"text": "^surface: painted$"}}
]
```

## Gotchas

- The text is the surface's content, so the step proves that the surface renders what it holds. It does not prove the background color. Take a screenshot and look at it for that.
- A brush that code reads from the style comes back in the theme the app started in. A component window's surface follows a theme switch, so paint with a surface rather than the brush.
- A style that leaves `Background` null paints Windows' solid background, in the window's light or dark theme.
- A contrast theme wins over the style's colors: the background becomes Windows' window color.
