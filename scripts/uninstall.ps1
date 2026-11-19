#requires -Version 5.1

function Write-WinshellLine([string] $Message) { [Console]::Out.WriteLine($Message) }

function Assert-WinshellTestIsolation {
    if ($env:WINSHELL_INSTALL_TEST -ne '1') { return }
    if (-not $env:WINSHELL_USER_PATH_FILE -or -not $env:WINSHELL_AUTOSTART_FILE) {
        throw 'A test must set WINSHELL_USER_PATH_FILE and WINSHELL_AUTOSTART_FILE.'
    }
    $real = [Environment]::GetFolderPath('LocalApplicationData'), [Environment]::GetFolderPath('ApplicationData')
    foreach ($folder in $env:LOCALAPPDATA, $env:APPDATA) {
        if (-not $folder -or $real -contains [IO.Path]::GetFullPath($folder).TrimEnd('\')) {
            throw 'Refusing to use the real LOCALAPPDATA or APPDATA from a test.'
        }
    }
}

function Assert-WinshellNotRunning([string] $Root, [string] $Again) {
    $prefix = $Root.TrimEnd('\') + '\'
    $ids = @(Get-CimInstance Win32_Process -Filter "Name = 'winshell.exe' OR Name = 'winshellw.exe'" |
            Where-Object { -not $_.ExecutablePath -or $_.ExecutablePath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) } |
            ForEach-Object { $_.ProcessId })
    if ($ids) { throw "Winshell is running (pid $($ids -join ', ')). Close it, then run the $Again again.`nNothing was changed." }
}

function Remove-WinshellFolder([string] $Path) {
    $item = Get-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    if (-not $item) { return $false }
    if ($item.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw "Refusing to delete $Path because it is a link." }
    Remove-Item -LiteralPath $Path -Recurse -Force
    return $true
}

function Get-WinshellUserPath {
    if ($env:WINSHELL_USER_PATH_FILE) {
        if (Test-Path -LiteralPath $env:WINSHELL_USER_PATH_FILE) { return [IO.File]::ReadAllText($env:WINSHELL_USER_PATH_FILE) }
        return ''
    }
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment')
    if (-not $key) { return '' }
    try { return [string]$key.GetValue('Path', '', 'DoNotExpandEnvironmentNames') } finally { $key.Dispose() }
}

function Set-WinshellUserPath([string] $Value) {
    if ($env:WINSHELL_USER_PATH_FILE) {
        [IO.File]::WriteAllText($env:WINSHELL_USER_PATH_FILE, $Value)
        return
    }
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Environment')
    try {
        $kind = 'ExpandString'
        if ($key.GetValueNames() -contains 'Path' -and $key.GetValueKind('Path') -eq 'String') { $kind = 'String' }
        $key.SetValue('Path', $Value, $kind)
    } finally { $key.Dispose() }
    if (-not ('Winshell.Install.Native' -as [type])) {
        Add-Type -Namespace Winshell.Install -Name Native -MemberDefinition '[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, UIntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);'
    }
    $result = [UIntPtr]::Zero
    [void][Winshell.Install.Native]::SendMessageTimeout([IntPtr]0xffff, 0x1A, [UIntPtr]::Zero, 'Environment', 2, 5000, [ref]$result)
}

function Uninstall-Winshell {
    $ErrorActionPreference = 'Stop'
    Assert-WinshellTestIsolation
    $root = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\winshell'))
    $bin = Join-Path $root 'bin'
    Assert-WinshellNotRunning $root 'uninstaller'
    $removed = Remove-WinshellFolder $root
    if ($removed) { Write-WinshellLine "Removed $root." }

    $parts = @((Get-WinshellUserPath).Split(';') | Where-Object { $_ })
    $kept = @($parts | Where-Object { $_.TrimEnd('\') -ne $bin })
    if ($kept.Count -ne $parts.Count) {
        Write-WinshellLine "Removing $bin from your PATH."
        Set-WinshellUserPath ($kept -join ';')
        $removed = $true
    }
    $session = @("$env:Path".Split(';') | Where-Object { $_ })
    $keptSession = @($session | Where-Object { $_.TrimEnd('\') -ne $bin })
    if (-not $env:WINSHELL_USER_PATH_FILE -and $keptSession.Count -ne $session.Count) { $env:Path = $keptSession -join ';' }

    $shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Winshell.lnk'
    if ((Test-Path -LiteralPath $shortcut) -and
        (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut).TargetPath -in (Join-Path $bin 'winshell.exe'), (Join-Path $bin 'winshellw.exe')) {
        Write-WinshellLine 'Removing the Start menu shortcut.'
        Remove-Item -LiteralPath $shortcut -Force
        $removed = $true
    }
    $ours = "(?i)(^|[\s""])$([regex]::Escape($bin))\\winshellw?\.exe($|[\s""])"
    if ($env:WINSHELL_AUTOSTART_FILE) {
        if ((Test-Path -LiteralPath $env:WINSHELL_AUTOSTART_FILE) -and [IO.File]::ReadAllText($env:WINSHELL_AUTOSTART_FILE) -match $ours) {
            Write-WinshellLine 'Removing the sign-in entry.'
            Remove-Item -LiteralPath $env:WINSHELL_AUTOSTART_FILE -Force
            $removed = $true
        }
    } else {
        $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run', $true)
        if ($key) {
            try {
                if ([string]$key.GetValue('Winshell') -match $ours) {
                    Write-WinshellLine 'Removing the sign-in entry.'
                    $key.DeleteValue('Winshell')
                    $removed = $true
                }
            } finally { $key.Dispose() }
        }
    }

    if ($removed) {
        Write-WinshellLine 'Winshell is uninstalled. Your shells in %APPDATA%\winshell were left in place. Logs and state stay in %LOCALAPPDATA%\winshell.'
    } else {
        Write-WinshellLine 'Winshell is not installed.'
    }
}

if ($ExecutionContext.SessionState.LanguageMode -ne 'FullLanguage') {
    Write-Error -ErrorAction Continue "uninstall.ps1 needs PowerShell's full language mode, and a system policy such as WDAC or AppLocker keeps this session in $($ExecutionContext.SessionState.LanguageMode). Delete %LOCALAPPDATA%\Programs\winshell by hand, then remove its PATH entry and Start menu shortcut."
    if ($PSCommandPath) { exit 1 }
} else {
    try {
        Uninstall-Winshell
    } catch {
        [Console]::Error.WriteLine($_.Exception.Message)
        if ($PSCommandPath) { exit 1 }
    }
}
