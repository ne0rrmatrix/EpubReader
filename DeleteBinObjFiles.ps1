<#
.SYNOPSIS
    Removes all 'bin' and 'obj' directories recursively from the current folder.
.DESCRIPTION
    Useful for cleaning up .NET build artifacts to reduce disk space.
.PARAMETER DryRun
    If set, it will only list the folders that would be deleted without removing them.
.EXAMPLE
    .\Clean-BuildArtifacts.ps1
    .\Clean-BuildArtifacts.ps1 -DryRun
#>

param(
    [switch]$DryRun
)

$directoriesToDelete = Get-ChildItem -Recurse -Directory | Where-Object { $_.Name -eq "bin" -or $_.Name -eq "obj" }

if ($directoriesToDelete.Count -eq 0) {
    Write-Host "No 'bin' or 'obj' folders found to remove." -ForegroundColor Yellow
    return
}

Write-Host "Found $($directoriesToDelete.Count) directories to process..." -ForegroundColor Cyan

foreach ($dir in $directoriesToDelete) {
    if ($DryRun) {
        Write-Host "[DRY RUN] Would delete: $($dir.FullName)" -ForegroundColor Red
    } 
    else {
        try {
            # Attempt to remove. Force ignores read-only attributes.
            # ErrorAction SilentlyContinue hides errors for files locked by Visual Studio.
            Remove-Item $dir.FullName -Recurse -Force -ErrorAction SilentlyContinue
            
            # Verify if it is gone (helpful logging)
            if (-not (Test-Path $dir.FullName)) {
                Write-Host "Deleted: $($dir.FullName)" -ForegroundColor Green
            } 
            else {
                Write-Warning "Could not delete $($dir.FullName). It may be in use by Visual Studio or a process."
            }
        }
        catch {
            Write-Error "Failed to remove $($dir.FullName) : $_"
        }
    }
}

if ($DryRun) {
    Write-Host "`nDry run complete. Run without -DryRun to actually delete files." -ForegroundColor Cyan
} 
else {
    Write-Host "`nCleanup finished." -ForegroundColor Green
}
