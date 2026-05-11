# Installer for the lux CLI on Windows (PowerShell 5.1+).
#
# Detects the host architecture (x64 / arm64), downloads the matching release
# archive from https://github.com/LuaLux/lux/releases/latest, extracts it to
# %LOCALAPPDATA%\Lux, and appends that directory to the user PATH so `lux`
# works in every new shell. Honour these env vars / parameters to override:
#   $env:LUX_INSTALL_DIR  - where to extract the archive (default %LOCALAPPDATA%\Lux)
#   $env:LUX_VERSION      - install a specific tag        (default: latest)
#
# Usage: irm https://raw.githubusercontent.com/LuaLux/lux/main/scripts/install.ps1 | iex

#Requires -Version 5.1
[CmdletBinding()]
param(
    [string] $InstallDir = $(if ($env:LUX_INSTALL_DIR) { $env:LUX_INSTALL_DIR } else { Join-Path $env:LOCALAPPDATA "Lux" }),
    [string] $Tag        = $(if ($env:LUX_VERSION) { $env:LUX_VERSION } else { "latest" })
)

$ErrorActionPreference = "Stop"
$ProgressPreference    = "SilentlyContinue"   # speeds up Invoke-WebRequest significantly

$repo = "LuaLux/lux"

function Write-Info($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Err($msg)  { Write-Host "error: $msg" -ForegroundColor Red; exit 1 }

$arch = switch ($env:PROCESSOR_ARCHITECTURE) {
    "AMD64" { "x64" }
    "ARM64" { "arm64" }
    default { Write-Err "Unsupported architecture: $($env:PROCESSOR_ARCHITECTURE)" }
}

$archive = "lux-win-$arch.zip"

if ($Tag -eq "latest") {
    Write-Info "Resolving latest release tag..."
    try {
        $headers = @{ "User-Agent" = "lux-installer"; "Accept" = "application/vnd.github+json" }
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" -Headers $headers
        $Tag = $release.tag_name
    } catch {
        Write-Err "Could not query the GitHub API: $($_.Exception.Message)"
    }
    if (-not $Tag) { Write-Err "Could not determine latest release tag." }
}

$url = "https://github.com/$repo/releases/download/$Tag/$archive"
Write-Info "Downloading $archive ($Tag)..."

$tmp = New-Item -ItemType Directory -Path (Join-Path $env:TEMP "lux-install-$([System.Guid]::NewGuid().ToString('N'))") -Force
try {
    $zipPath = Join-Path $tmp $archive
    try {
        Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing
    } catch {
        Write-Err "Download failed: $url`n$($_.Exception.Message)"
    }

    # Best-effort checksum verification (release ships a .sha256 sibling).
    try {
        $shaPath = "$zipPath.sha256"
        Invoke-WebRequest -Uri "$url.sha256" -OutFile $shaPath -UseBasicParsing -ErrorAction Stop
        Write-Info "Verifying checksum..."
        $expected = ((Get-Content $shaPath -Raw) -split '\s+')[0].ToLower()
        $actual   = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLower()
        if ($expected -ne $actual) {
            Write-Err "Checksum mismatch: expected $expected, got $actual"
        }
    } catch [System.Net.WebException] {
        # No .sha256 file alongside the archive — skip verification.
    } catch {
        # Anything else from the verification step is fatal.
        if ($_.Exception.Message -notmatch "Not Found") { throw }
    }

    if (Test-Path $InstallDir) {
        Write-Info "Cleaning existing $InstallDir..."
        Remove-Item -Path $InstallDir -Recurse -Force
    }
    Write-Info "Extracting to $InstallDir..."
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Expand-Archive -Path $zipPath -DestinationPath $InstallDir -Force

    # Append InstallDir to the user PATH (HKCU). User-scope so no admin needed.
    $userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    $entries  = if ($userPath) { $userPath.Split([System.IO.Path]::PathSeparator) } else { @() }
    if ($entries -notcontains $InstallDir) {
        $newPath = if ($userPath) { "$userPath;$InstallDir" } else { $InstallDir }
        [Environment]::SetEnvironmentVariable("PATH", $newPath, "User")
        Write-Info "Added $InstallDir to user PATH."
        Write-Info "Restart your shell (or VSCode / your IDE) for new sessions to see lux."
    } else {
        Write-Info "$InstallDir already on user PATH."
    }

    # Make `lux` work in the current shell too, without forcing a restart.
    if ($env:PATH -notlike "*$InstallDir*") {
        $env:PATH = "$env:PATH;$InstallDir"
    }
} finally {
    Remove-Item -Path $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Info "lux $Tag installed."
Write-Info "Try: lux version"
