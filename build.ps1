#!/usr/bin/env pwsh
# Build script for EpubReader with Firebase secrets supplied via env, google.json, or parameters.
# Secrets are not printed and are NOT written to committed C# files.

[CmdletBinding()]
param(
    [string]$ApiKey,
    [string]$AuthDomain,
    [string]$DatabaseUrl,
    [string]$GoogleJsonPath,
    [string]$Configuration = "Debug",
    [switch]$Android,
    [switch]$Windows,
    [switch]$DebugBuild,
    [switch]$ReleaseBuild
)

$ErrorActionPreference = "Stop"

# Honor -DebugBuild / -ReleaseBuild switches (mutually exclusive) and override the Configuration parameter
if ($DebugBuild -and $ReleaseBuild) {
    throw "Specify only one of -DebugBuild or -ReleaseBuild."
}
if ($DebugBuild) {
    $Configuration = 'Debug'
} elseif ($ReleaseBuild) {
    $Configuration = 'Release'
}

# Resolve workspace root relative to this script
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Solution = Join-Path $RepoRoot "EpubReader.slnx"

function Parse-GoogleJsonAndSetEnv([string]$path)
{
    if (-not (Test-Path $path)) { return $false }
    try {
        $google = Get-Content -Raw $path | ConvertFrom-Json
    } catch {
        return $false
    }

    # Best-effort parsing for the common structure
    try {
        $key = $google.client[0].api_key[0].current_key
    } catch {
        $key = $null
    }

    if ($key -and -not $env:FIREBASE_API_KEY) { $env:FIREBASE_API_KEY = $key }
    if ($google.project_info -and $google.project_info.project_id -and -not $env:FIREBASE_AUTH_DOMAIN) {
        $env:FIREBASE_AUTH_DOMAIN = "$($google.project_info.project_id).firebaseapp.com"
    }
    if ($google.project_info -and $google.project_info.firebase_url -and -not $env:FIREBASE_DATABASE_URL) {
        $env:FIREBASE_DATABASE_URL = $google.project_info.firebase_url
    }

    return $true
}

# If a google-services.json path is supplied, parse it and set env vars.
if ($GoogleJsonPath) {
    if (-not (Test-Path $GoogleJsonPath)) {
        throw "Google JSON file not found at path: $GoogleJsonPath"
    }

    # Parse and set env vars from provided file (do not copy)
    if (-not (Parse-GoogleJsonAndSetEnv -path $GoogleJsonPath)) {
        throw "Failed to parse google json at path: $GoogleJsonPath"
    }
}

# Apply explicit parameters to environment when provided (highest precedence)
if ($ApiKey) { $env:FIREBASE_API_KEY = $ApiKey }
if ($AuthDomain) { $env:FIREBASE_AUTH_DOMAIN = $AuthDomain }
if ($DatabaseUrl) { $env:FIREBASE_DATABASE_URL = $DatabaseUrl }

# If any required env vars are still missing, attempt to read from build-secrets folder
$missing = @()
if (-not $env:FIREBASE_API_KEY) { $missing += "FIREBASE_API_KEY" }
if (-not $env:FIREBASE_AUTH_DOMAIN) { $missing += "FIREBASE_AUTH_DOMAIN" }
if (-not $env:FIREBASE_DATABASE_URL) { $missing += "FIREBASE_DATABASE_URL" }

if ($missing.Count -gt 0) {
    # Candidate locations inside the repo
    $candidatePaths = @(
        (Join-Path $RepoRoot 'build-secrets\google-services.json'),
        (Join-Path $RepoRoot 'build-secrets\android\google-services.json'),
        (Join-Path $RepoRoot 'EpubReader\build-secrets\google-services.json'),
        (Join-Path $RepoRoot 'EpubReader\build-secrets\android\google-services.json')
    )

    foreach ($p in $candidatePaths) {
        if (Test-Path $p) {
            # Only set missing values, do not override existing env vars
            Parse-GoogleJsonAndSetEnv -path $p | Out-Null
            # Recompute missing
            $missing = @()
            if (-not $env:FIREBASE_API_KEY) { $missing += "FIREBASE_API_KEY" }
            if (-not $env:FIREBASE_AUTH_DOMAIN) { $missing += "FIREBASE_AUTH_DOMAIN" }
            if (-not $env:FIREBASE_DATABASE_URL) { $missing += "FIREBASE_DATABASE_URL" }
            if ($missing.Count -eq 0) { break }
        }
    }
}

# Final validation without echoing values
$missing = @()
if (-not $env:FIREBASE_API_KEY) { $missing += "FIREBASE_API_KEY" }
if (-not $env:FIREBASE_AUTH_DOMAIN) { $missing += "FIREBASE_AUTH_DOMAIN" }
if (-not $env:FIREBASE_DATABASE_URL) { $missing += "FIREBASE_DATABASE_URL" }

if ($missing.Count -gt 0) {
    throw "Missing required environment variables: $($missing -join ', '). Provide parameters, set env vars, or place google-services.json in build-secrets and try again."
}

# Determine target framework based on switches
if ($Android -and $Windows) {
    throw "Specify only one target platform: -Android or -Windows."
}

[string]$targetFramework = $null
if ($Android) {
    $targetFramework = 'net10.0-android'
} elseif ($Windows) {
    # Use the Windows TFM declared in the project; adjust if your project uses a different TFM
    $targetFramework = 'net10.0-windows10.0.19041.0'
}

Write-Host "Building EpubReader (Configuration=$Configuration)" -ForegroundColor Cyan
if ($targetFramework) {
    Write-Host "Targeting framework: $targetFramework" -ForegroundColor Cyan
    & dotnet build $Solution -c $Configuration -p:TargetFramework=$targetFramework
} else {
    # No specific platform requested; build all targets in the solution
    & dotnet build $Solution -c $Configuration
}

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

Write-Host "Build succeeded." -ForegroundColor Green