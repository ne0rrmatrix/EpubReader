<#
.SYNOPSIS
  Export an Android keystore (.jks/.keystore) to a Base64 string for storing as a GitHub secret.

.DESCRIPTION
  Reads a binary keystore file and prints a single-line Base64 representation.
  Optionally writes the Base64 to a file or copies it to the Windows clipboard.

.EXAMPLE
  .\export-keystore-base64.ps1
  (uses default %USERPROFILE%\.android\debug.keystore and writes base64 to stdout)

.EXAMPLE
  .\export-keystore-base64.ps1 -KeystorePath C:\keys\release.jks -OutFile release.jks.b64 -ToClipboard -Overwrite
#>

[CmdletBinding()]
param(
    [string]
    $KeystorePath = "$env:USERPROFILE\.android\debug.keystore",

    [string]
    $OutFile = '',

    [switch]
    $ToClipboard,

    [switch]
    $Overwrite
)

if (-not (Test-Path -Path $KeystorePath)) {
    Write-Error "Keystore not found: $KeystorePath"
    exit 1
}

try {
    $bytes = [System.IO.File]::ReadAllBytes($KeystorePath)
    $b64 = [System.Convert]::ToBase64String($bytes)
} catch {
    Write-Error "Failed to read or encode keystore: $_"
    exit 1
}

if ($OutFile) {
    if ((Test-Path -Path $OutFile) -and -not $Overwrite) {
        Write-Error "Output file '$OutFile' already exists. Use -Overwrite to replace."
        exit 1
    }
    try {
        Set-Content -Path $OutFile -Value $b64 -Encoding ascii
        Write-Host "Wrote Base64 to: $OutFile"
    } catch {
        Write-Error "Failed to write output file: $_"
        exit 1
    }
} else {
    Write-Output $b64
}

if ($ToClipboard) {
    try {
        if (Get-Command -Name Set-Clipboard -ErrorAction SilentlyContinue) {
            $b64 | Set-Clipboard
            Write-Host "Base64 copied to clipboard."
        } else {
            Write-Warning "Set-Clipboard not available in this session. Install PSReadLine / use Windows PowerShell or provide -OutFile."
        }
    } catch {
        Write-Warning "Failed to copy to clipboard: $_"
    }
}

# Exit successfully
exit 0
