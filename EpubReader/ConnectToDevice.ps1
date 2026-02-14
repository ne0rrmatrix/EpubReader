# Check if adb is in the System PATH
if (-not (Get-Command adb -ErrorAction SilentlyContinue)) {
    Write-Error "ADB not found. Please install Android Platform Tools and add it to your PATH."
    return
}

Write-Host "Searching for connected Android devices..." -ForegroundColor Cyan

# Check for connected devices
$devices = adb devices | Select-String -Pattern "\tdevice$"

if ($devices.Count -eq 0) {
    Write-Host "No devices found. Please ensure USB Debugging is enabled and the cable is connected." -ForegroundColor Red
    return
}

Write-Host "Device detected! Starting logcat..." -ForegroundColor Green
Write-Host "Press Ctrl+C to stop the log stream." -ForegroundColor Yellow

# Clear the previous buffer to get fresh logs
adb logcat -c

# Start streaming logs
# Optional: Use --pid to filter by a specific process ID if known
adb logcat
