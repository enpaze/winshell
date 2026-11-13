# Winshell

A desktop shell toolkit for Windows 11. Write your own taskbar, launcher or dock with C# and WinUI 3, and Winshell builds and runs it.

Installing Winshell gives you no taskbar: it runs the shell you write. Writing one needs the [.NET SDK](https://dotnet.microsoft.com/download).

## Install with PowerShell

```powershell
irm https://github.com/enpaze/winshell/releases/latest/download/install.ps1 | iex
```

This installs the `winshell` command for your user, without administrator rights. Or download the x64 or arm64 zip from [Releases](https://github.com/enpaze/winshell/releases) and extract it.

Releases are currently unsigned. Custom configurations may break between releases until 1.0.

## Get started

```powershell
winshell new                # list the starter and the examples
winshell new starter        # copy the starter into your config folder
winshell run -c starter     # build it and run it
winshell kill               # stop it
```

`winshell new <name>` copies any of the examples the same way. The starter is a bar with a clock and a battery, and the [configuration guide](docs/configuration.md) begins by reading it file by file. Until there is a configuration, `winshell run` starts nothing.

If something goes wrong, run `winshell doctor` or read [troubleshooting](docs/troubleshooting.md).

[Commands](docs/reference/command-line.md) · [API reference](docs/reference/index.md) · [MIT license](LICENSE)
