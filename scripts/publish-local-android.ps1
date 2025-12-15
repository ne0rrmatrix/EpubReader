#!/usr/bin/env pwsh
<#
Publish a local signed Android artifact using the keystore configured in .env.local

Usage:
  ./scripts/publish-local-android.ps1         # loads .env.local and publishes release build
  ./scripts/publish-local-android.ps1 -EnvFile path\to\.env.local

Notes:
- This script only supports local keystore-based signing. It will NOT decode any
  base64 secrets or use CI secrets. Keep your .env.local gitignored.
- It calls EpubReader\publishAndroid.ps1 which reads the following env vars:
  ANDROID_KEYSTORE, ANDROID_KEYSTORE_PASSWORD, ANDROID_KEYSTORE_ALIAS, ANDROID_KEY_PASSWORD
#>

[CmdletBinding()]
param(
    [string]$EnvFile = "$PSScriptRoot\..\.env.local"
)

function Load-EnvFile([string]$path) {
    if (-not (Test-Path $path)) {
        Write-Host "Env file not found at $path" -ForegroundColor Yellow
        return $false
    }
    Write-Host "Loading environment variables from $path" -ForegroundColor Cyan
    $lines = Get-Content $path -ErrorAction Stop
    foreach ($line in $lines) {
        if ($line -match '^\s*([^#=\s]+)=(.*)$') {
            $k = $matches[1].Trim()
            $v = $matches[2].Trim()
            if (-not [string]::IsNullOrWhiteSpace($v)) {
                # Do not echo values
                Set-Item -Path "env:$k" -Value $v
            }
        }
    }
    return $true
}

if (-not (Load-EnvFile -path $EnvFile)) {
    throw "Failed to load environment variables. Create .env.local with your ANDROID_* values or pass -EnvFile."
}

if (-not $env:ANDROID_KEYSTORE) {
    throw "ANDROID_KEYSTORE is not set. Put ANDROID_KEYSTORE in .env.local pointing to your keystore file."
}

if (-not (Test-Path $env:ANDROID_KEYSTORE)) {
    throw "Keystore file not found at $env:ANDROID_KEYSTORE"
}

Write-Host "Confirmed keystore at $env:ANDROID_KEYSTORE. Running local publish (will sign)." -ForegroundColor Cyan

# Call the project-local publish script which reads the same env vars
& "$PSScriptRoot\..\EpubReader\publishAndroid.ps1"

if ($LASTEXITCODE -ne 0) { throw "publishAndroid.ps1 failed with exit code $LASTEXITCODE" }

Write-Host "Local Android publish completed. Artifacts are in ./artifacts/android" -ForegroundColor Green
