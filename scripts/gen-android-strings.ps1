param(
    [string]$GoogleServicesJson = "$PSScriptRoot\..\build-secrets\google-services.json",
    [string]$OutFile = "$PSScriptRoot\..\build-secrets\android\strings.secrets.xml"
)

if (-not (Test-Path $GoogleServicesJson)) {
    Write-Error "google-services.json not found at $GoogleServicesJson. Run this script after placing your local google-services.json at ./build-secrets/google-services.json"
    exit 1
}

$json = Get-Content $GoogleServicesJson -Raw | ConvertFrom-Json

# Ensure output folder exists
$OutDir = Split-Path $OutFile -Parent
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$apiKey = $json.client[0].api_key[0].current_key
$appId = $json.client[0].client_info.mobilesdk_app_id
$projectInfo = $json.project_info

$webClientId = ($json.client[0].oauth_client | Where-Object { $_.client_type -eq 3 } | Select-Object -First 1 -ExpandProperty client_id) -or ''

$firebaseDatabaseUrl = $projectInfo.firebase_url -or $projectInfo.project_id
$projectId = $projectInfo.project_id -or ''
$projectNumber = $projectInfo.project_number -or ''
$storageBucket = $projectInfo.storage_bucket -or ''
$firebaseAuthDomain = if ($projectId) { "$projectId.firebaseapp.com" } else { '' }

@"
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <string name="google_api_key">$($apiKey -replace '&','&amp;')</string>
    <string name="firebase_auth_domain">$($firebaseAuthDomain -replace '&','&amp;')</string>
    <string name="firebase_database_url">$($firebaseDatabaseUrl -replace '&','&amp;')</string>
    <string name="google_app_id">$($appId -replace '&','&amp;')</string>
    <string name="default_web_client_id">$($webClientId -replace '&','&amp;')</string>
    <string name="project_id">$($projectId -replace '&','&amp;')</string>
    <string name="project_number">$($projectNumber -replace '&','&amp;')</string>
    <string name="google_storage_bucket">$($storageBucket -replace '&','&amp;')</string>
</resources>
"@ | Out-File -FilePath $OutFile -Encoding utf8

Write-Host "Generated $OutFile"