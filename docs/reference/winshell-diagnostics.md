# Winshell.Diagnostics

The checks `winshell doctor` prints, which a shell can also run for itself.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## CheckStatus

| Value | Number |
| --- | --- |
| `Pass` | 0 |
| `Warn` | 1 |
| `Fail` | 2 |

## Doctor

| Member | Type |
| --- | --- |
| `ForThisProcess(string configRoot)` | IReadOnlyList of [DoctorCheck](#doctorcheck), static |
| `StatusName([CheckStatus](#checkstatus) status)` | string, static |

## DoctorCheck

| Member | Type |
| --- | --- |
| `DoctorCheck(string Id, string Title, [CheckStatus](#checkstatus) Status, string Message, string? Fix = null)` | constructor |
| `Fix` | string?, init |
| `Id` | string, init |
| `Message` | string, init |
| `Status` | [CheckStatus](#checkstatus), init |
| `Title` | string, init |
