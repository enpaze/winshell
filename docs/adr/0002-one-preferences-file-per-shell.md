# One preferences file per shell

Each shell keeps the user's choices in one TOML file, `preferences\<shell id>.toml` in the config folder. Each component, such as a bar or a widget area outside a bar, has a section named after its `Name`, and the configuration's own preferences are the keys before the first section. A component declares its section as a typed class, with the live reload, defaults written as comments and checks that `TomlFile` gives a whole file today. A user has one file to edit for each shell, and a new composition adds a section rather than a file with a format of its own.

```toml
clock24 = true            # the configuration's own

[bar]                     # the component named "bar"
edge = "top"
end = [{ widget = "battery" }, { widget = "clock" }]

[[bar.screen]]            # one screen that differs
manufacturer = "DEL"
model = "DELL U2720Q"
serial = "ABC123"
edge = "left"
```

The file replaces the placement file, `widgets\<shell id>.toml`, and any file a configuration kept for itself, such as the Omarchy example's `bar\<shell id>.toml`. Nothing reads the old files: Winshell is before 1.0, and the changelog says to move the lists by hand.

## Considered options

- **A file for each feature, as before.** Each composition would add a file and a format, and a user would hunt across them.
- **Reading the old files once and writing the new one.** Migration code for a few users before 1.0.

## Consequences

- A named `WidgetArea` is a component: its section holds one `widgets` list. The placement file as a separate idea goes away.
- Proposed name, settled in review: `Preferences`, the type a section derives from, as in `class BarPreferences : Preferences`.
