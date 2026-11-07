# Winshell

A desktop shell toolkit for Windows 11. Write your own taskbar, launcher or dock with C# and WinUI 3, and Winshell builds and runs it.

Installing Winshell gives you no taskbar: it runs the shell you write. Writing one needs the .NET SDK.

## Install with PowerShell

```powershell
irm https://github.com/enpaze/winshell/releases/latest/download/install.ps1 | iex
```

Installs the `winshell` command for your user, without administrator rights. Or download the x64 or arm64 zip from [Releases](https://github.com/enpaze/winshell/releases) and extract it.

Releases are currently unsigned. Custom configurations may break between releases until 1.0.

## Get started

`winshell new` lists the starter and the examples, and `winshell new <name>` copies one into your config folder. Run it with `winshell run -c <name>`. Until there is a configuration, `winshell run` starts nothing. The [configuration guide](docs/configuration.md) builds a shell up from one panel.

Stop a shell with `winshell kill`. If something goes wrong, run `winshell doctor` or read [troubleshooting](docs/troubleshooting.md).

[Commands](docs/reference/command-line.md) · [API reference](docs/reference/index.md) · [Contributing](CONTRIBUTING.md) · [MIT license](LICENSE)
