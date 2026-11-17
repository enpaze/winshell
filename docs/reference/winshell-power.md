# Winshell.Power

The battery, the power mode, monitor brightness, idle time and keeping the PC awake. Reach them from `Shell.Current.Power`, `Shell.Current.PowerMode` and `Shell.Current.Brightness`. An idle watch is `Shell.NewIdleMonitor()`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## BrightnessConnection

| Value | Number |
| --- | --- |
| `Internal` | 0 |
| `External` | 1 |

## BrightnessMonitor

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Brightness` | int |
| `Connection` | [BrightnessConnection](#brightnessconnection), read-only |
| `DeviceName` | string, read-only |
| `Maximum` | int, read-only |
| `Minimum` | int, read-only |
| `Name` | string, read-only |

## BrightnessService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Monitors` | ObservableList of [BrightnessMonitor](#brightnessmonitor), read-only |
| `Ready` | bool, read-only |
| `Dispose()` | void |

## EffectivePowerMode

| Value | Number |
| --- | --- |
| `EnergySaver` | 0 |
| `BetterBattery` | 1 |
| `Balanced` | 2 |
| `HighPerformance` | 3 |
| `MaxPerformance` | 4 |
| `GameMode` | 5 |
| `MixedReality` | 6 |
| `Unknown` | -1 |

## IdleMonitor

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Enabled` | bool |
| `IsIdle` | bool, read-only |
| `RespectKeepAwake` | bool |
| `Timeout` | double |
| `Dispose()` | void |

## KeepAwake

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `KeepAwake([Log](winshell.md#log) log)` | constructor |
| `Enabled` | bool |
| `Dispose()` | void |

## PowerDevice

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `ChargeRate` | double, read-only |
| `DeviceId` | string, read-only |
| `FullChargeCapacity` | double, read-only |
| `HealthPercentage` | double, read-only |
| `HealthSupported` | bool, read-only |
| `IsPresent` | bool, read-only |
| `IsSystemBattery` | bool, read-only |
| `Model` | string, read-only |
| `Percentage` | double, read-only |
| `PowersTheSystem` | bool, read-only |
| `Ready` | bool, read-only |
| `RemainingCapacity` | double, read-only |
| `State` | [PowerDeviceState](#powerdevicestate), read-only |
| `TimeToEmpty` | double, read-only |
| `TimeToFull` | double, read-only |
| `Type` | [PowerDeviceType](#powerdevicetype), read-only |

## PowerDeviceState

| Value | Number |
| --- | --- |
| `Unknown` | 0 |
| `Charging` | 1 |
| `Discharging` | 2 |
| `Empty` | 3 |
| `FullyCharged` | 4 |
| `NotCharging` | 5 |

## PowerDeviceType

| Value | Number |
| --- | --- |
| `Unknown` | 0 |
| `Battery` | 2 |

## PowerModeLevel

| Value | Number |
| --- | --- |
| `BestEfficiency` | 0 |
| `Balanced` | 1 |
| `BestPerformance` | 2 |
| `Unknown` | 3 |

## PowerModeService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Available` | bool, read-only |
| `Effective` | [EffectivePowerMode](#effectivepowermode), read-only |
| `Mode` | [PowerModeLevel](#powermodelevel) |
| `Ready` | bool, read-only |
| `Dispose()` | void |

## PowerService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Devices` | ObservableList of [PowerDevice](#powerdevice), read-only |
| `DisplayDevice` | [PowerDevice](#powerdevice), read-only |
| `OnBattery` | bool, read-only |
| `Dispose()` | void |
