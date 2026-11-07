# Winshell.Cpu

How busy the processors are. Reach it from `Shell.Current.Cpu`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## CpuService

How busy the processors are. Windows is read every 2 seconds while something listens to PropertyChanged, and not at all while nothing does.

| Member | Type | Description |
| --- | --- | --- |
| `Usage` | double, read-only | The share of the last 2 seconds the processors were busy, across all cores, from 0 to 100. 0 while nothing listens, and for the first 2 seconds after something starts to. |
| `PropertyChanged` | PropertyChangedEventHandler | Raised on the loop's thread when Usage changes. The first handler added starts reading Windows, and removing the last stops it. Add and remove handlers on the loop's thread. |
| `Dispose()` | void | Stops reading Windows, and a handler added later does not start it again. |
