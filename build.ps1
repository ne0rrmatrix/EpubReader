#!/usr/bin/env pwsh
# Build script for EpubReader with Firebase secrets supplied via env, secrets JSON, or parameters.
# Secrets are not printed and are NOT written to committed C# files.

[CmdletBinding()]
param(
    [string]$ApiKey,
    [string]$AuthDomain,
    [string]$DatabaseUrl,
    
    [string]$GoogleSecretB64File,
    [switch]$ValidateOnly,
    [string]$Configuration = "Debug",
    [switch]$Android,
    [switch]$Windows,
    [switch]$DebugBuild,
    [switch]$ReleaseBuild
    ,
    [switch]$APK,
    [switch]$AAB,
    [switch]$Signed,
    [switch]$Release
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

# Alias: -Release maps to Configuration
if ($Release) { $Configuration = 'Release' }

# Resolve workspace root relative to this script
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Solution = Join-Path $RepoRoot "EpubReader.slnx"

function Parse-SecretsJsonAndSetEnv([string]$path)
{
    if (-not (Test-Path $path)) { return $false }
    try {
        $json = Get-Content -Raw $path | ConvertFrom-Json
    } catch {
        return $false
    }

    # Best-effort parsing for the common structure used previously (keep resilient)
    try {
        $key = $json.client[0].api_key[0].current_key
    } catch {
        $key = $null
    }

    if ($key -and -not $env:FIREBASE_API_KEY) { $env:FIREBASE_API_KEY = $key }
    if ($json.project_info -and $json.project_info.project_id -and -not $env:FIREBASE_AUTH_DOMAIN) {
        $env:FIREBASE_AUTH_DOMAIN = "$($json.project_info.project_id).firebaseapp.com"
    }
    if ($json.project_info -and $json.project_info.firebase_url -and -not $env:FIREBASE_DATABASE_URL) {
        $env:FIREBASE_DATABASE_URL = $json.project_info.firebase_url
    }

    return $true
}

function Parse-GoogleJsonToMsBuildProps([string]$path) {
    if (-not (Test-Path $path)) { return '' }
    try {
        $json = Get-Content -Raw $path | ConvertFrom-Json
    } catch {
        return ''
    }

    $firebaseApiKey = $null
    $firebaseAuthDomain = $null
    $firebaseDatabaseUrl = $null

    try { $firebaseApiKey = $json.client[0].api_key[0].current_key } catch { }
    try { if ($json.project_info.project_id) { $firebaseAuthDomain = "$($json.project_info.project_id).firebaseapp.com" } } catch { }
    try { $firebaseDatabaseUrl = $json.project_info.firebase_url } catch { }

    $props = @()
    if ($firebaseApiKey) { $props += "/p:FirebaseApiKey=$firebaseApiKey" }
    if ($firebaseAuthDomain) { $props += "/p:FirebaseAuthDomain=$firebaseAuthDomain" }
    if ($firebaseDatabaseUrl) { $props += "/p:FirebaseDatabaseUrl=$firebaseDatabaseUrl" }

    return ($props -join ' ')
}

# (Removed direct local google config path handling to avoid referencing committed filenames.)

# The build now requires a canonical `build-secrets\google-services.json`.
# Parse it if present to populate FIREBASE_* environment variables used at build time.
# Legacy fallbacks (secrets.b64, secrets.json, secrets.decoded.json) have been removed.
$canonical1 = Join-Path $RepoRoot 'build-secrets\google-services.json'
$canonical2 = Join-Path $RepoRoot 'EpubReader\build-secrets\google-services.json'
if (Test-Path $canonical1) { Parse-SecretsJsonAndSetEnv -path $canonical1 | Out-Null }
elseif (Test-Path $canonical2) { Parse-SecretsJsonAndSetEnv -path $canonical2 | Out-Null }

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
    # Only consider the canonical google-services.json locations
    $candidatePaths = @(
        (Join-Path $RepoRoot 'build-secrets\google-services.json'),
        (Join-Path $RepoRoot 'EpubReader\build-secrets\google-services.json')
    )

    foreach ($p in $candidatePaths) {
        if (Test-Path $p) {
            # Only set missing values, do not override existing env vars
            Parse-SecretsJsonAndSetEnv -path $p | Out-Null
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
    throw "Missing required environment variables: $($missing -join ', '). Provide parameters, set env vars, or place the canonical build-secrets\google-services.json and try again."
}

# If requested, validate only (decode/parse secrets and verify env vars) then exit successfully without building.
if ($ValidateOnly) {
    Write-Host "Validation succeeded (ValidateOnly). Required FIREBASE_* variables are present." -ForegroundColor Green
    exit 0
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
# Ensure obj\GeneratedFiles exists for all projects to avoid CS0016 long-path/sourcegen write errors
function Ensure-GeneratedFilesDirectories() {
    try {
        $csprojFiles = Get-ChildItem -Path $RepoRoot -Filter *.csproj -Recurse -ErrorAction SilentlyContinue
        foreach ($f in $csprojFiles) {
            $projDir = Split-Path -Parent $f.FullName
            $genDir = Join-Path $projDir 'obj\GeneratedFiles'
            if (-not (Test-Path $genDir)) {
                New-Item -Path $genDir -ItemType Directory -Force | Out-Null
                Write-Host "Created GeneratedFiles directory: $genDir" -ForegroundColor DarkGreen
            }
        }
    } catch {
        Write-Host "Warning: failed to ensure GeneratedFiles directories: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}
Ensure-GeneratedFilesDirectories
# Prepare MSBuild properties from canonical google-services.json if present
$msbuildProps = ''
$canonicalCandidates = @(
    (Join-Path $RepoRoot 'build-secrets\google-services.json'),
    (Join-Path $RepoRoot 'EpubReader\build-secrets\google-services.json')
)
foreach ($c in $canonicalCandidates) {
    if (Test-Path $c) { $msbuildProps = Parse-GoogleJsonToMsBuildProps -path $c; break }
}

Write-Host "Building EpubReader (Configuration=$Configuration)" -ForegroundColor Cyan
if ($targetFramework) {
    Write-Host "Targeting framework: $targetFramework" -ForegroundColor Cyan
    $tfmProp = "/p:TargetFramework=$targetFramework"
    $cmd = "dotnet build `"$Solution`" -c $Configuration $tfmProp $msbuildProps"
    Write-Host "Running: $cmd"
    iex $cmd
} else {
    # No specific platform requested; build all targets in the solution
    $cmd = "dotnet build `"$Solution`" -c $Configuration $msbuildProps"
    Write-Host "Running: $cmd"
    iex $cmd
}

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

Write-Host "Build succeeded." -ForegroundColor Green

# Note: MSBuild/Csproj now provisions canonical google-services.json from build-secrets and
# will remove it after build/publish. Script-level staging/cleanup was removed.

# If Android target was requested, produce a publish output and sign it when keystore env vars are present
if ($Android) {
    $projPath = Join-Path $RepoRoot 'EpubReader\EpubReader.csproj'
    $outDir = Join-Path $RepoRoot 'artifacts\android'

    $packageFormatProps = @()
    if ($APK) { $packageFormatProps += '/p:AndroidPackageFormat=apk' }
    elseif ($AAB) { $packageFormatProps += '/p:AndroidPackageFormat=aab' }

    # If signing is requested, allow providing signing inputs via a signingCode file in build-secrets
    # The signingCode file may be JSON or simple key=value lines. Supported keys (case-insensitive):
    # ANDROID_KEYSTORE, ANDROID_KEYSTORE_B64, ANDROID_KEYSTORE_PASSWORD, ANDROID_KEY_ALIAS, ANDROID_KEY_PASSWORD
    $decodedKeystore = $null

    if ($Signed) {
        $signingCodeCandidates = @(
            (Join-Path $RepoRoot 'build-secrets\signingCode.txt')
        )

        foreach ($sc in $signingCodeCandidates) {
            if (Test-Path $sc) {
                try {
                    $raw = Get-Content -Raw -Path $sc -ErrorAction Stop
                } catch { continue }

                $parsed = $null
                try {
                    $parsed = $raw | ConvertFrom-Json -ErrorAction Stop
                } catch {
                    $parsed = $null
                }

                if ($parsed) {
                    # JSON keys may be named variably; accept common aliases
                    if ($parsed.ANDROID_KEYSTORE) { $env:ANDROID_KEYSTORE = $parsed.ANDROID_KEYSTORE }
                    if ($parsed.keystore) { $env:ANDROID_KEYSTORE = $parsed.keystore }
                    if ($parsed.ANDROID_KEYSTORE_B64) { $env:ANDROID_KEYSTORE_B64 = $parsed.ANDROID_KEYSTORE_B64 }
                    if ($parsed.keystoreB64) { $env:ANDROID_KEYSTORE_B64 = $parsed.keystoreB64 }
                    if ($parsed.ANDROID_KEYSTORE_PASSWORD) { $env:ANDROID_KEYSTORE_PASSWORD = $parsed.ANDROID_KEYSTORE_PASSWORD }
                    if ($parsed.storePassword) { $env:ANDROID_KEYSTORE_PASSWORD = $parsed.storePassword }
                    if ($parsed.ANDROID_KEY_ALIAS) { $env:ANDROID_KEYSTORE_ALIAS = $parsed.ANDROID_KEY_ALIAS }
                    if ($parsed.keyAlias) { $env:ANDROID_KEYSTORE_ALIAS = $parsed.keyAlias }
                    if ($parsed.ANDROID_KEY_PASSWORD) { $env:ANDROID_KEY_PASSWORD = $parsed.ANDROID_KEY_PASSWORD }
                    if ($parsed.keyPassword) { $env:ANDROID_KEY_PASSWORD = $parsed.keyPassword }
                } else {
                    # Try parse key=value lines
                    $lines = $raw -split "\r?\n"
                    foreach ($l in $lines) {
                        if ($l -match '^\s*([^#=\s]+)\s*=\s*(.*)$') {
                            $k = $matches[1].Trim()
                            $v = $matches[2].Trim()
                            switch -Regex ($k.ToUpperInvariant()) {
                                'ANDROID_KEYSTORE' { if (-not $env:ANDROID_KEYSTORE) { $env:ANDROID_KEYSTORE = $v } }
                                'ANDROID_KEYSTORE_B64' { if (-not $env:ANDROID_KEYSTORE_B64) { $env:ANDROID_KEYSTORE_B64 = $v } }
                                'KEYSTOREB64' { if (-not $env:ANDROID_KEYSTORE_B64) { $env:ANDROID_KEYSTORE_B64 = $v } }
                                'ANDROID_KEYSTORE_PASSWORD' { if (-not $env:ANDROID_KEYSTORE_PASSWORD) { $env:ANDROID_KEYSTORE_PASSWORD = $v } }
                                'STOREPASSWORD' { if (-not $env:ANDROID_KEYSTORE_PASSWORD) { $env:ANDROID_KEYSTORE_PASSWORD = $v } }
                                'ANDROID_KEY_ALIAS' { if (-not $env:ANDROID_KEYSTORE_ALIAS) { $env:ANDROID_KEYSTORE_ALIAS = $v } }
                                'KEYALIAS' { if (-not $env:ANDROID_KEYSTORE_ALIAS) { $env:ANDROID_KEYSTORE_ALIAS = $v } }
                                'ANDROID_KEY_PASSWORD' { if (-not $env:ANDROID_KEY_PASSWORD) { $env:ANDROID_KEY_PASSWORD = $v } }
                                'KEYPASSWORD' { if (-not $env:ANDROID_KEY_PASSWORD) { $env:ANDROID_KEY_PASSWORD = $v } }
                                default { }
                            }
                        }
                    }
                }

                # If the signingCode provided a base64 keystore, decode it now if ANDROID_KEYSTORE not already set
                if (-not $env:ANDROID_KEYSTORE -and $env:ANDROID_KEYSTORE_B64) {
                    try {
                        $decodedKeystore = Join-Path $RepoRoot 'build-secrets\android.keystore'
                        [System.IO.File]::WriteAllBytes($decodedKeystore, [System.Convert]::FromBase64String($env:ANDROID_KEYSTORE_B64.Trim()))
                        $env:ANDROID_KEYSTORE = $decodedKeystore
                        Write-Host "Decoded keystore from signingCode to $decodedKeystore" -ForegroundColor Cyan
                    } catch {
                        Write-Host "Failed to decode keystore from signingCode: $($_.Exception.Message)" -ForegroundColor Yellow
                    }
                }

                break
            }
        }
    }

    # If signing is requested and no keystore path is provided, try to decode build-secrets/android.keystore.b64
    if ($Signed -and -not $env:ANDROID_KEYSTORE) {
        $keystoreCandidates = @(
            (Join-Path $RepoRoot 'build-secrets\android.keystore.b64'),
            (Join-Path $RepoRoot 'build-secrets\android\android.keystore.b64'),
            (Join-Path $RepoRoot 'EpubReader\build-secrets\android.keystore.b64')
        )
        foreach ($k in $keystoreCandidates) {
            if (Test-Path $k) {
                try {
                    $b64 = Get-Content -Raw -Path $k
                    if (-not [string]::IsNullOrWhiteSpace($b64)) {
                        $decodedKeystore = Join-Path $RepoRoot 'build-secrets\android.keystore'
                        [System.IO.File]::WriteAllBytes($decodedKeystore, [System.Convert]::FromBase64String($b64.Trim()))
                        $env:ANDROID_KEYSTORE = $decodedKeystore
                        Write-Host "Decoded keystore to $decodedKeystore" -ForegroundColor Cyan
                        break
                    }
                } catch {
                    Write-Host "Failed to decode keystore at ${k}: $($_.Exception.Message)" -ForegroundColor Yellow
                }
            }
        }
    }

    if ($env:ANDROID_KEYSTORE -and (Test-Path $env:ANDROID_KEYSTORE)) {
        Write-Host "Publishing signed Android artifact to $outDir"
        & dotnet publish $projPath -c $Configuration -f net10.0-android -o $outDir `
            $packageFormatProps `
            /p:AndroidKeyStore=true `
            /p:AndroidSigningKeyStore="$env:ANDROID_KEYSTORE" `
            /p:AndroidSigningStorePass="$env:ANDROID_KEYSTORE_PASSWORD" `
            /p:AndroidSigningKeyAlias="$env:ANDROID_KEYSTORE_ALIAS" `
            /p:AndroidSigningKeyPass="$env:ANDROID_KEY_PASSWORD" `
            $msbuildProps
    } else {
        if ($Signed) {
            Write-Host "Signing requested but no keystore found. Publishing unsigned Android artifact to $outDir" -ForegroundColor Yellow
        } else {
            Write-Host "Publishing unsigned Android artifact to $outDir"
        }
        & dotnet publish $projPath -c $Configuration -f net10.0-android -o $outDir `
            $packageFormatProps `
            $msbuildProps
    }

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
    Write-Host "Android publish completed." -ForegroundColor Green

    # Cleanup decoded keystore if we created one
    if ($decodedKeystore -and (Test-Path $decodedKeystore)) {
        try { Remove-Item -Path $decodedKeystore -ErrorAction SilentlyContinue }
        catch { }
    }

    # Note: MSBuild/Csproj handles provisioning and cleanup of google-services.json
}