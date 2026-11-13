# Tools CI installs by URL, and the waits that match them.
# Dependabot does not see a raw download. A bump is one edit of this file.
# NuGet pins in Directory.Packages.props and the SDK pin in global.json stay with Dependabot.
@{
    # One version, two archives: the Windows zip the quality job installs, and the Linux archive links.yml installs.
    # A rate-limited link is retried after retryWaitSeconds, maxRetries times, and then fails.
    # The Windows download of the archive waits downloadRetrySeconds between attempts, downloadMaxRetries times.
    lychee = @{
        version = '0.24.2'
        windowsSha256 = '32975d1493ee1a975d6bb41e4fb56fe419cb442ded628bb772ba2e614acfacad'
        linuxSha256 = '1f4e0ef7f6554a6ed33dd7ac144fb2e1bbed98598e7af973042fc5cd43951c9a'
        retryWaitSeconds = 10
        maxRetries = 4
        downloadRetrySeconds = 15
        downloadMaxRetries = 5
    }
    actionlint = @{
        version = '1.7.12'
    }
    # The newest stable release the pinned zizmor-action's lock can run. That action's default follows the lock,
    # and the lock moves when the action pin moves.
    zizmor = @{
        version = '1.31.1'
    }
    pester = @{
        version = '5.6.1'
        sha256 = '37b3749f69accead67c366d81508977f62afc7cbb9c4db88c3a255a1f0b20da5'
    }
    # wingetcreate v1.12.13.0. Two downloads: the exe the release job submits with, and
    # Microsoft.WindowsPackageManager.Utils 1.12.350, the validator that exe is built with.
    # package.ps1 writes the manifest schema that version validates. winget-pkgs merges that schema.
    wingetCreate = @{
        url = 'https://github.com/microsoft/winget-create/releases/download/v1.12.13.0/wingetcreate.exe'
        sha256 = '24042BD37915805615E6CF969AC57C6439124C3FE85823327F5F3FB24BD9FFEA'
    }
    wingetUtil = @{
        url = 'https://api.nuget.org/v3-flatcontainer/microsoft.windowspackagemanager.utils/1.12.350/microsoft.windowspackagemanager.utils.1.12.350.nupkg'
        sha256 = 'cb7786683a7ce395467850efabba506203889f9c71b395d0d9a26753410eaa58'
    }
}
