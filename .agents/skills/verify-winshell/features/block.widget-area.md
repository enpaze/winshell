# WidgetArea

A `WidgetArea` is a component that makes a widget for each placement in its list, in order, and passes each one its settings. Each bar has three. In the `features` shell, an area with one placement of the `feature` widget, with the setting `text = "placed"`, sits inside the surface, and that widget shows `widget area: placed`.

## Sub-features

- `place` makes each registered widget a placement names, in order, and gives it the running `Shell`, its window, the placement's settings and the area's orientation.
- `settings` passes a placement's other keys, nested tables included, as `Settings`.
- `orientation` lays the widgets out across or down, from `Orientation`.
- `preferences` lets the user replace a named area's whole list in the `widgets` key of its section of the preferences file, and applies a save live.
- `replace` disposes a widget whose name leaves its place, and renders a widget that stays again with its new settings.
- `failed-widget` shows a placeholder with the name and the reason for an unknown widget or one that throws, while the other widgets keep running.
- `style` styles the plain controls its widgets render from the shell's style, or its own `Style`.

## How to get to it (user POV)

- In a Reactor component, return `WidgetArea("end", [new("message", new Dictionary<string, object> { ["text"] = "Hello" })])`.
- Register each widget it places with `Widgets.Register<T>("name")` in `Start`.
- Every `Bar` has a start, a center and an end area, from its `Start`, `Center` and `End` lists.
- Edit the area's section of `preferences\<shell id>.toml`, such as `[end]` with `widgets = [...]`, and save it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.

- **Read the placed widget.** Start the feature shell and look inside the surface. Run `winshell inspect --pid {pid} tree`. Exactly one element has the automation id `block.widget-area`, and its `text` is `widget area: placed`, matching `^widget area: placed$`, which proves the area made the `feature` widget and passed it the placement's `text` setting.

```json
[
  {"element": "block.widget-area", "has": {"text": "^widget area: placed$"}}
]
```

## Gotchas

- The feature area has no name, so it always uses its defaults and has no section in the preferences file. The step does not prove the preferences path.
- Widget names are case-sensitive. A name that no widget is registered under shows a placeholder, and the reason is logged under `widgets`; `winshell log` prints it.
- A name registered twice fails when the shell starts.
- A preferences list with a syntax error, or without string `widget` names, keeps the last valid list and logs the file location under `preferences`.
