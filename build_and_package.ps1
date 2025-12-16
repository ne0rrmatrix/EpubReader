<#
build_and_package.ps1
Generates Windows and Android builds, zips artifacts, and creates a markdown report listing files and checksums.

Usage:
  pwsh ./scripts/build_and_package.ps1 [-RepoRoot <path>] [-KeystorePath <path>] [-AllowAndroidFailure]

Notes:
- Defaults assume this script lives in the repository under ./scripts and the solution is in the repo root.
- Android signing keystore default: <RepoRoot>/EpubReader/EpubReader.keystore
- Android publish failures are tolerated when -AllowAndroidFailure is used.
#>
param(
    [string]$RepoRoot = $(Resolve-Path "$PSScriptRoot" | Select-Object -ExpandProperty Path),
    [string]$KeystorePath = '',
    [switch]$AllowAndroidFailure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $KeystorePath) {
    $KeystorePath = Join-Path -Path $RepoRoot -ChildPath 'EpubReader\EpubReader.keystore'
}

Write-Host "Repository root: $RepoRoot"
Write-Host "Keystore path: $KeystorePath"

$artifactsDir = Join-Path $RepoRoot 'artifacts'
$windowsOut = Join-Path $artifactsDir 'windows'
$androidOut = Join-Path $artifactsDir 'android'

# Ensure artifacts directories exist
New-Item -Path $windowsOut -ItemType Directory -Force | Out-Null
New-Item -Path $androidOut -ItemType Directory -Force | Out-Null

$solutionProj = Join-Path $RepoRoot 'EpubReader\EpubReader.csproj'
if (-not (Test-Path $solutionProj)) {
    Write-Error "Project file not found: $solutionProj"
    exit 2
}

# Verify dotnet is available and print diagnostics
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "'dotnet' executable not found in PATH. Ensure the .NET SDK is installed and 'dotnet' is on PATH."
    exit 3
}
Write-Host "dotnet --info:" 
try { dotnet --info } catch { Write-Warning "Failed to run 'dotnet --info': $_" }

# Ensure NuGet packages are restored so project.assets.json exists before builds/publishes
Write-Host "Restoring NuGet packages for solution: $solutionProj"
try {
    & dotnet restore $solutionProj
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet restore failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
} catch {
    Write-Warning "dotnet restore encountered an error: $_"
}

function Run-Command {
    param([string]$Cmd, [string[]]$Args)
    $argString = ''
    if ($Args) { $argString = $Args -join ' ' }
    Write-Host "Running: $Cmd $argString"
    # Capture output for diagnostics
    $output = & $Cmd @Args 2>&1
    $code = $LASTEXITCODE
    if ($output) { Write-Host $output }
    if ($code -ne 0) { Write-Host "Exit code: $code" }
    return $code
}

# Parse canonical google-services.json into MSBuild properties for packaging
function Get-MsBuildPropsFromGoogleJson([string]$repoRoot) {
    $candidates = @(
        (Join-Path $repoRoot 'build-secrets\google-services.json'),
        (Join-Path $repoRoot 'EpubReader\build-secrets\google-services.json')
    )
    foreach ($p in $candidates) {
        if (Test-Path $p) {
            try {
                $j = Get-Content -Raw -Path $p | ConvertFrom-Json
            } catch { continue }
            $props = @()
            try { if ($j.client[0].api_key[0].current_key) { $props += "/p:FirebaseApiKey=$($j.client[0].api_key[0].current_key)" } } catch {}
            try { if ($j.project_info.project_id) { $props += "/p:FirebaseAuthDomain=$($j.project_info.project_id).firebaseapp.com" } } catch {}
            try { if ($j.project_info.firebase_url) { $props += "/p:FirebaseDatabaseUrl=$($j.project_info.firebase_url)" } } catch {}
            return $props
        }
    }
    return @()
}

$msbuildProps = Get-MsBuildPropsFromGoogleJson -repoRoot $RepoRoot

# 1) Build (windows) - keep as in user's template
$buildArgs = @(
    'build', $solutionProj,
    '-c','Release',
    
    '/p:TreatWarningsAsErrors=false',
    '/p:WarningLevel=0'
)
$buildArgs += $msbuildProps
Write-Host "Running: dotnet $($buildArgs -join ' ')"
& dotnet @buildArgs
$code = $LASTEXITCODE
if ($code -ne 0) {
    Write-Error "dotnet build failed with exit code $code"
    exit $code
}

# 2) Publish windows
$publishWinArgs = @(
    'publish', $solutionProj,
    '-c','Release',
    '-f','net10.0-windows10.0.19041.0',
    '-o',$windowsOut,
    '/p:TreatWarningsAsErrors=false',
    '/p:WarningLevel=0'
)
$publishWinArgs += $msbuildProps
Write-Host "Running: dotnet $($publishWinArgs -join ' ')"
& dotnet @publishWinArgs
$code = $LASTEXITCODE
if ($code -ne 0) {
    Write-Error "dotnet publish (windows) failed with exit code $code"
    exit $code
}

# 3) Publish android (allow failure optionally)
$publishAndroidArgs = @(
    'publish', $solutionProj,
    '-c','Release',
    '-f','net10.0-android',
    
    '-o',$androidOut,
    '/p:AndroidKeyStore=true',
    "/p:AndroidSigningKeyStore=$KeystorePath",
    '/p:AndroidSigningStorePass=Jqbywx5Jqbywx5',
    '/p:AndroidSigningKeyAlias=EpubReader',
    '/p:AndroidSigningKeyPass=Jqbywx5Jqbywx5'
)
$publishAndroidArgs += $msbuildProps
Write-Host "Running: dotnet $($publishAndroidArgs -join ' ')"
& dotnet @publishAndroidArgs
$code = $LASTEXITCODE
if ($code -ne 0) {
    if ($AllowAndroidFailure.IsPresent) {
        Write-Warning "dotnet publish (android) failed with exit code $code, continuing because -AllowAndroidFailure was specified."
    } else {
        Write-Error "dotnet publish (android) failed with exit code $code"
        # continue anyway if user wanted permissive behavior was not specified? follow user's example with || true, so continue but warn
        Write-Warning "Continuing despite Android publish failure (to mimic provided behavior). Use -AllowAndroidFailure to suppress the error exit."
    }
}

# 4) Zip artifacts
$windowsZip = Join-Path $artifactsDir 'windows.zip'
$androidZip = Join-Path $artifactsDir 'android.zip'

if (Test-Path $windowsZip) { Remove-Item $windowsZip -Force }
Compress-Archive -Path (Join-Path $windowsOut '*') -DestinationPath $windowsZip -Force
Write-Host "Created $windowsZip"

# Note: MSBuild/Csproj handles provisioning and cleanup of google-services.json

# For Android, prefer APK/AAB files if present; otherwise zip whole dir
$apkFiles = Get-ChildItem -Path $androidOut -Recurse -File -Include *.apk,*.aab -ErrorAction SilentlyContinue
if ($apkFiles -and $apkFiles.Count -gt 0) {
    if (Test-Path $androidZip) { Remove-Item $androidZip -Force }
    # Create a temp folder to copy apks then zip
    $tempApkDir = Join-Path $env:TEMP ([Guid]::NewGuid().ToString())
    New-Item -Path $tempApkDir -ItemType Directory | Out-Null
    foreach ($f in $apkFiles) { Copy-Item -Path $f.FullName -Destination $tempApkDir }
    Compress-Archive -Path (Join-Path $tempApkDir '*') -DestinationPath $androidZip -Force
    Remove-Item -Path $tempApkDir -Recurse -Force
    Write-Host "Created $androidZip with APK/AAB files"
} else {
    if (Test-Path $androidZip) { Remove-Item $androidZip -Force }
    Compress-Archive -Path (Join-Path $androidOut '*') -DestinationPath $androidZip -Force
    Write-Host "Created $androidZip (zipped entire android output dir)"
}

# 5) Generate markdown report with file lists, sizes, SHA256
$reportPath = Join-Path $artifactsDir 'packaging_report.md'
$reportLines = @()
$reportLines += "# Packaging report"
$reportLines += "Generated: $(Get-Date -Format o)"
$reportLines += ""

function Add-DirectoryReport {
    param([string]$Name, [string]$Path)
    $reportLines += "## $Name"
    if (-not (Test-Path $Path)) {
        $reportLines += "Directory not found: $Path"
        $reportLines += ""
        return
    }
    $files = Get-ChildItem -Path $Path -Recurse -File | Sort-Object FullName
    if (-not $files) {
        $reportLines += "(no files)"
        $reportLines += ""
        return
    }
    $reportLines += "| Path | Size (bytes) | SHA256 |"
    $reportLines += "|---|---:|---|"
    foreach ($f in $files) {
        $rel = $f.FullName.Substring($Path.Length).TrimStart('\','/')
        try {
            $hash = (Get-FileHash -Path $f.FullName -Algorithm SHA256 -ErrorAction Stop).Hash
        } catch {
            $hash = 'ERROR'
        }
        $reportLines += "| $rel | $($f.Length) | $hash |"
    }
    $reportLines += ""
}

Add-DirectoryReport -Name 'Windows artifact contents' -Path $windowsOut
Add-DirectoryReport -Name 'Android artifact contents' -Path $androidOut

# Also include zip file checksums
$reportLines += "## Zip files"
$reportLines += "| Zip | Size (bytes) | SHA256 |"
$reportLines += "|---|---:|---|"
foreach ($z in @($windowsZip, $androidZip)) {
    if (Test-Path $z) {
        $info = Get-Item $z
        $hash = (Get-FileHash -Path $z -Algorithm SHA256).Hash
        $reportLines += "| $(Split-Path $z -Leaf) | $($info.Length) | $hash |"
    } else {
        $reportLines += "| $(Split-Path $z -Leaf) | MISSING | - |"
    }
}

# Write report
$reportLines | Out-File -FilePath $reportPath -Encoding UTF8
Write-Host "Wrote report: $reportPath"

Write-Host "Done. Review the zips and report in: $artifactsDir"
