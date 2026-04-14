[CmdletBinding()]
param(
    [switch]$Android,
    [switch]$Windows,
    [switch]$Ios,
    [switch]$MacCatalyst,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration,
    [switch]$DebugBuild,
    [switch]$ReleaseBuild,
    [string]$ApiKey,
    [string]$AuthDomain,
    [string]$DatabaseUrl,
    [string]$GoogleJsonPath,
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Info {
    param([string]$Message)

    Write-Host $Message -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)

    Write-Host $Message -ForegroundColor Green
}

function Write-WarningMessage {
    param([string]$Message)

    Write-Host $Message -ForegroundColor Yellow
}

function Resolve-BuildConfiguration {
    if ($DebugBuild -and $ReleaseBuild) {
        throw 'Specify only one of -DebugBuild or -ReleaseBuild.'
    }

    if ($DebugBuild) {
        return 'Debug'
    }

    if ($ReleaseBuild) {
        return 'Release'
    }

    if (-not [string]::IsNullOrWhiteSpace($Configuration)) {
        return $Configuration
    }

    return 'Debug'
}

function Resolve-ProjectPath {
    $projectPath = Join-Path $PSScriptRoot 'EpubReader\EpubReader.csproj'
    if (-not (Test-Path -LiteralPath $projectPath)) {
        throw "Could not find project file at '$projectPath'."
    }

    return $projectPath
}

function Resolve-AndroidGoogleServicesDestination {
    return Join-Path $PSScriptRoot 'EpubReader\Resources\Raw\google-services.json'
}

function Resolve-CandidatePath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $PSScriptRoot $Path
}

function Resolve-GoogleServicesPath {
    param([string]$ExplicitPath)

    $candidates = @()

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $candidates += (Resolve-CandidatePath -Path $ExplicitPath)
    }

    $candidates += @(
        (Join-Path $PSScriptRoot 'build-secrets\google-services.json'),
        (Join-Path $PSScriptRoot 'build-secrets\android\google-services.json'),
        (Join-Path $PSScriptRoot 'EpubReader\build-secrets\google-services.json'),
        (Join-Path $PSScriptRoot 'EpubReader\build-secrets\android\google-services.json'),
        (Resolve-AndroidGoogleServicesDestination)
    )

    foreach ($candidate in $candidates) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path -LiteralPath $candidate)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return $null
}

function Get-FirebaseSettingsFromGoogleServices {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return @{}
    }

    $json = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    $projectId = $json.project_info.project_id
    $firebaseUrl = $json.project_info.firebase_url
    $apiKey = $json.client[0].api_key[0].current_key

    $settings = @{}
    if (-not [string]::IsNullOrWhiteSpace($apiKey)) {
        $settings['FIREBASE_API_KEY'] = $apiKey
    }

    if (-not [string]::IsNullOrWhiteSpace($projectId)) {
        $settings['FIREBASE_AUTH_DOMAIN'] = "$projectId.firebaseapp.com"
    }

    if (-not [string]::IsNullOrWhiteSpace($firebaseUrl)) {
        $settings['FIREBASE_DATABASE_URL'] = $firebaseUrl
    }

    return $settings
}

function Set-FirebaseEnvironment {
    param(
        [hashtable]$GoogleJsonSettings,
        [string]$ApiKeyValue,
        [string]$AuthDomainValue,
        [string]$DatabaseUrlValue
    )

    foreach ($entry in $GoogleJsonSettings.GetEnumerator()) {
        Set-Item -Path "Env:$($entry.Key)" -Value $entry.Value
    }

    if (-not [string]::IsNullOrWhiteSpace($ApiKeyValue)) {
        Set-Item -Path 'Env:FIREBASE_API_KEY' -Value $ApiKeyValue
    }

    if (-not [string]::IsNullOrWhiteSpace($AuthDomainValue)) {
        Set-Item -Path 'Env:FIREBASE_AUTH_DOMAIN' -Value $AuthDomainValue
    }

    if (-not [string]::IsNullOrWhiteSpace($DatabaseUrlValue)) {
        Set-Item -Path 'Env:FIREBASE_DATABASE_URL' -Value $DatabaseUrlValue
    }
}

function Resolve-TargetFrameworks {
    param([bool]$HasPlatformSelection)

    if (-not $HasPlatformSelection) {
        return @()
    }

    $frameworks = [System.Collections.Generic.List[string]]::new()

    if ($Android) {
        $frameworks.Add('net10.0-android')
    }

    if ($Windows) {
        $frameworks.Add('net10.0-windows10.0.19041.0')
    }

    if ($Ios) {
        $frameworks.Add('net10.0-ios')
    }

    if ($MacCatalyst) {
        $frameworks.Add('net10.0-maccatalyst')
    }

    return $frameworks
}

function Invoke-DotNetBuild {
    param(
        [string]$ProjectPath,
        [string]$BuildConfiguration,
        [string[]]$TargetFrameworks
    )

    if ($TargetFrameworks.Count -eq 0) {
        $arguments = @('build', $ProjectPath, '-c', $BuildConfiguration)
        if ($NoRestore) {
            $arguments += '--no-restore'
        }

        Write-Info "Building all supported target frameworks with configuration '$BuildConfiguration'."
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }

        return
    }

    foreach ($targetFramework in $TargetFrameworks) {
        $arguments = @('build', $ProjectPath, '-c', $BuildConfiguration, '-f', $targetFramework)
        if ($NoRestore) {
            $arguments += '--no-restore'
        }

        Write-Info "Building '$targetFramework' with configuration '$BuildConfiguration'."
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }
}

$projectPath = Resolve-ProjectPath
$buildConfiguration = Resolve-BuildConfiguration
$hasPlatformSelection = $Android -or $Windows -or $Ios -or $MacCatalyst
$targetFrameworks = Resolve-TargetFrameworks -HasPlatformSelection:$hasPlatformSelection
$requiresAndroidConfig = $Android -or -not $hasPlatformSelection
$resolvedGoogleJsonPath = Resolve-GoogleServicesPath -ExplicitPath $GoogleJsonPath
$googleJsonSettings = Get-FirebaseSettingsFromGoogleServices -Path $resolvedGoogleJsonPath

Set-FirebaseEnvironment -GoogleJsonSettings $googleJsonSettings -ApiKeyValue $ApiKey -AuthDomainValue $AuthDomain -DatabaseUrlValue $DatabaseUrl

$stagedAndroidGoogleServices = $false
$backupGoogleServicesPath = $null
$androidGoogleServicesDestination = Resolve-AndroidGoogleServicesDestination

try {
    if ($requiresAndroidConfig) {
        if ([string]::IsNullOrWhiteSpace($resolvedGoogleJsonPath)) {
            Write-WarningMessage 'No google-services.json was found in the documented locations. Android builds will use the existing project asset if available.'
        }
        else {
            $resolvedSourcePath = (Resolve-Path -LiteralPath $resolvedGoogleJsonPath).Path
            $resolvedDestinationPath = $null
            if (Test-Path -LiteralPath $androidGoogleServicesDestination) {
                $resolvedDestinationPath = (Resolve-Path -LiteralPath $androidGoogleServicesDestination).Path
            }

            if ($resolvedSourcePath -ne $resolvedDestinationPath) {
                $destinationDirectory = Split-Path -Parent $androidGoogleServicesDestination
                if (-not (Test-Path -LiteralPath $destinationDirectory)) {
                    New-Item -ItemType Directory -Path $destinationDirectory | Out-Null
                }

                if (Test-Path -LiteralPath $androidGoogleServicesDestination) {
                    $backupGoogleServicesPath = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
                    Copy-Item -LiteralPath $androidGoogleServicesDestination -Destination $backupGoogleServicesPath -Force
                }

                Copy-Item -LiteralPath $resolvedGoogleJsonPath -Destination $androidGoogleServicesDestination -Force
                $stagedAndroidGoogleServices = $true
                Write-Info 'Staged android google-services.json for this build.'
            }
        }
    }

    Invoke-DotNetBuild -ProjectPath $projectPath -BuildConfiguration $buildConfiguration -TargetFrameworks $targetFrameworks
    Write-Success 'Build completed successfully.'
}
finally {
    if ($stagedAndroidGoogleServices) {
        if (-not [string]::IsNullOrWhiteSpace($backupGoogleServicesPath) -and (Test-Path -LiteralPath $backupGoogleServicesPath)) {
            Copy-Item -LiteralPath $backupGoogleServicesPath -Destination $androidGoogleServicesDestination -Force
            Remove-Item -LiteralPath $backupGoogleServicesPath -Force
        }
        else {
            Remove-Item -LiteralPath $androidGoogleServicesDestination -Force -ErrorAction SilentlyContinue
        }

        Write-Info 'Restored the original android google-services.json asset.'
    }
}
