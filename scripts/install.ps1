#requires -Version 5.1

param(
    [string] $Version,
    [ValidateSet('stable', 'dev')]
    [string] $Channel = 'stable',
    [switch] $AutoStart
)

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

function Receive-WinshellFile([string] $Uri, [string] $Path) {
    try {
        Invoke-WebRequest -Uri $Uri -OutFile $Path -UseBasicParsing
    } catch {
        throw "Could not download $Uri. $($_.Exception.Message)"
    }
}

function Get-WinshellDevTag([string] $Base) {
    $list = if ($Base -eq 'https://github.com/enpaze/winshell/releases') { 'https://api.github.com/repos/enpaze/winshell/releases?per_page=30' } else { "$Base/list" }
    try {
        $releases = Invoke-RestMethod -Uri $list -Headers @{ Accept = 'application/vnd.github+json'; 'User-Agent' = 'winshell' } -UseBasicParsing
    } catch {
        throw "Could not download $list. $($_.Exception.Message)"
    }
    $newest = @($releases | ForEach-Object { $_ } |
            Where-Object { $_.draft -ne $true -and $_.prerelease -eq $true -and "$($_.tag_name)" -cmatch '^dev-[0-9a-f]{7}$' -and $_.published_at } |
            Sort-Object { [datetimeoffset]$_.published_at } -Descending)
    if (-not $newest) { throw 'No dev release was found.' }
    return $newest[0].tag_name
}

function Get-WinshellSignInCommand([string] $Exe) {
    @((Join-Path $env:SystemRoot 'System32\conhost.exe'), '--headless', $Exe, '--no-build', '--no-duplicate' |
            ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
}

function Install-Winshell([string] $Version, [string] $Channel, [switch] $AutoStart, [switch] $SessionKeepsPath) {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'
    Assert-WinshellTestIsolation
    $Channel = "$Channel".Trim().ToLowerInvariant()
    if (-not $Channel) { $Channel = "$env:WINSHELL_CHANNEL".Trim().ToLowerInvariant() }
    if (-not $Channel) { $Channel = 'stable' }
    if ($Channel -ne 'stable' -and $Channel -ne 'dev') { throw "Channel must be stable or dev, not '$Channel'." }
    if ($Channel -eq 'dev') {
        $Version = ''
    } else {
        $Version = "$Version".Trim()
        if (-not $Version) { $Version = "$env:WINSHELL_VERSION".Trim() }
        if ($Version) {
            $Version = $Version -replace '^v', ''
            if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must be x.y.z, not '$Version'." }
        }
    }
    $signIn = $AutoStart -or ("$env:WINSHELL_AUTOSTART".Trim() -match '^(1|true|yes)$')
    $arch = switch ([string][Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
        'X64' { 'x64' }
        'Arm64' { 'arm64' }
        default { throw "Winshell publishes win-x64 and win-arm64. This PC is '$_'." }
    }
    $base = 'https://github.com/enpaze/winshell/releases'
    if ($env:WINSHELL_RELEASE_BASE) {
        $base = $env:WINSHELL_RELEASE_BASE.Trim().TrimEnd('/')
        if ($base -ne 'https://github.com/enpaze/winshell/releases' -and
            $base -notmatch '^http://(127\.0\.0\.1|localhost)(:[0-9]{1,5})?(/[A-Za-z0-9._~/-]*)?$') {
            throw 'WINSHELL_RELEASE_BASE must be https://github.com/enpaze/winshell/releases or an http://127.0.0.1 test server.'
        }
    }
    $root = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\winshell'))
    $bin = Join-Path $root 'bin'
    $stage = Join-Path $root 'bin.new'
    $old = Join-Path $root 'bin.old'
    Assert-WinshellNotRunning $root 'installer'
    if ($Channel -eq 'dev') {
        Write-WinshellLine "Installing the dev Winshell (win-$arch)."
    } elseif ($Version) {
        Write-WinshellLine "Installing Winshell $Version (win-$arch)."
    } else {
        Write-WinshellLine "Installing the latest Winshell (win-$arch)."
    }
    if ($PSVersionTable.PSVersion.Major -lt 6) {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    }
    $tag = if ($Channel -eq 'dev') { Get-WinshellDevTag $base }
    $release = if ($Channel -eq 'dev') {
        "$base/download/$tag"
    } elseif ($Version) {
        "$base/download/v$Version"
    } else {
        "$base/latest/download"
    }
    $temp = Join-Path ([IO.Path]::GetTempPath()) ('winshell-install-' + [guid]::NewGuid().ToString('n'))
    New-Item -ItemType Directory -Path $temp | Out-Null
    try {
        Write-WinshellLine 'Downloading SHA256SUMS.txt.'
        Receive-WinshellFile "$release/SHA256SUMS.txt" (Join-Path $temp 'SHA256SUMS.txt')
        $name = if ($Channel -eq 'dev') {
            "winshell-.+-win-$arch\.zip"
        } elseif ($Version) {
            [regex]::Escape("winshell-$Version-win-$arch.zip")
        } else {
            "winshell-\d+\.\d+\.\d+-win-$arch\.zip"
        }
        $line = [regex]::Match([IO.File]::ReadAllText((Join-Path $temp 'SHA256SUMS.txt')), "(?m)^([0-9A-Fa-f]{64}) [ *]($name)\r?$")
        if (-not $line.Success) { throw "That release lists no win-$arch zip in SHA256SUMS.txt." }
        $package = $line.Groups[2].Value
        if ($package -notmatch '^winshell-(.+)-win-(x64|arm64)\.zip$') { throw "That release lists no win-$arch zip in SHA256SUMS.txt." }
        $installed = $Matches[1]
        Write-WinshellLine "Downloading $package."
        $zip = Join-Path $temp $package
        $folder = if ($Channel -eq 'dev') { $tag } else { "v$installed" }
        Receive-WinshellFile "$base/download/$folder/$package" $zip
        Write-WinshellLine 'Verifying SHA256.'
        $expected = $line.Groups[1].Value.ToLowerInvariant()
        $actual = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $expected) {
            throw "SHA256 mismatch for $package.`nExpected: $expected`nActual:   $actual`nThe download was discarded. Nothing was installed."
        }
        Remove-WinshellFolder $stage | Out-Null
        Remove-WinshellFolder $old | Out-Null
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::ExtractToDirectory($zip, $stage)
        foreach ($required in 'winshell.exe', 'winshellw.exe') {
            if (-not (Test-Path -LiteralPath (Join-Path $stage $required))) { throw "The archive does not contain $required. Nothing was installed." }
        }
        Write-WinshellLine "Installing to $bin."
        if (Test-Path -LiteralPath $bin) { Rename-Item -LiteralPath $bin -NewName 'bin.old' }
        Rename-Item -LiteralPath $stage -NewName 'bin'
    } finally {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $stage) { Remove-WinshellFolder $stage | Out-Null }
    }
    try {
        Remove-WinshellFolder $old | Out-Null
    } catch {
        Write-WinshellLine "The previous copy is still at $old. $($_.Exception.Message)"
    }

    if ($Channel -eq 'dev') {
        $saved = Start-Process -FilePath (Join-Path $bin 'winshell.exe') -ArgumentList @('update', '--channel', 'dev') -WindowStyle Hidden -Wait -PassThru
        if ($saved.ExitCode -ne 0) {
            throw "Winshell $installed is installed, and winshell update --channel dev failed with exit $($saved.ExitCode)."
        }
    } else {
        $shells = if ("$env:WINSHELL_SHELLS_DIR".Trim()) { [IO.Path]::GetFullPath("$env:WINSHELL_SHELLS_DIR".Trim()) } else { Join-Path $env:APPDATA 'winshell' }
        Remove-Item -LiteralPath (Join-Path $shells 'update.toml') -Force -ErrorAction SilentlyContinue
    }
    Start-Process -FilePath (Join-Path $bin 'winshellw.exe') -ArgumentList '--no-duplicate' -WindowStyle Hidden | Out-Null

    $parts = @((Get-WinshellUserPath).Split(';') | Where-Object { $_ })
    if ($parts | Where-Object { $_.TrimEnd('\') -eq $bin }) {
        Write-WinshellLine "$bin is already on your PATH."
    } else {
        Write-WinshellLine "Adding $bin to your PATH."
        Set-WinshellUserPath (($parts + $bin) -join ';')
    }
    if (-not $env:WINSHELL_USER_PATH_FILE -and -not ("$env:Path".Split(';') | Where-Object { $_.TrimEnd('\') -eq $bin })) {
        $env:Path = "$env:Path".TrimEnd(';') + ';' + $bin
    }

    Write-WinshellLine 'Creating the Start menu shortcut.'
    $programs = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    New-Item -ItemType Directory -Force -Path $programs | Out-Null
    $link = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $programs 'Winshell.lnk'))
    $link.TargetPath = Join-Path $bin 'winshellw.exe'
    $link.Arguments = '--no-duplicate'
    $link.WorkingDirectory = $bin
    $link.Description = 'Winshell'
    $link.Save()

    if ($signIn) {
        $command = Get-WinshellSignInCommand (Join-Path $bin 'winshell.exe')
        if ($env:WINSHELL_AUTOSTART_FILE) {
            [IO.File]::WriteAllText($env:WINSHELL_AUTOSTART_FILE, $command)
        } else {
            $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
            try { $key.SetValue('Winshell', $command) } finally { $key.Dispose() }
        }
        Write-WinshellLine 'Winshell will run when you sign in.'
    }

    if ($SessionKeepsPath) {
        Write-WinshellLine "Winshell $installed is installed. Run winshell."
    } else {
        Write-WinshellLine "Winshell $installed is installed. Open a new terminal and run winshell."
    }
    $store = @()
    if ($env:WINSHELL_INSTALL_TEST -ne '1') {
        try { $store = @(Get-AppxPackage -Name '*Winshell*' -ErrorAction Stop) } catch {
            $store = @()
            Write-WinshellLine "Could not check for a Microsoft Store copy of Winshell, so that check was skipped. $($_.Exception.Message)"
        }
    }
    if ($store.Count -gt 0) {
        Write-WinshellLine 'A Microsoft Store copy of Winshell is also installed. Its WindowsApps alias usually comes first on PATH, so winshell may start the Store app instead of this copy.'
    }
}

if ($ExecutionContext.SessionState.LanguageMode -ne 'FullLanguage') {
    Write-Error -ErrorAction Continue "install.ps1 needs PowerShell's full language mode, and a system policy such as WDAC or AppLocker keeps this session in $($ExecutionContext.SessionState.LanguageMode). Download the portable zip from https://github.com/enpaze/winshell/releases instead."
    if ($PSCommandPath) { exit 1 }
} else {
    try {
        $keeps = -not $PSCommandPath -or $MyInvocation.InvocationName -eq '&'
        if ($PSBoundParameters -and $PSBoundParameters.ContainsKey('Channel')) {
            Install-Winshell -Version $Version -Channel $Channel -AutoStart:$AutoStart -SessionKeepsPath:$keeps
        } else {
            Install-Winshell -Version $Version -AutoStart:$AutoStart -SessionKeepsPath:$keeps
        }
    } catch {
        [Console]::Error.WriteLine($_.Exception.Message)
        if ($PSCommandPath) { exit 1 }
    }
}
