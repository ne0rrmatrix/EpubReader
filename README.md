# EpubReader 📚

<div align="center">

**A versatile and user-friendly EPUB reader/viewer for Windows, Android, iOS, and macOS**

*Enjoy your favorite ebooks with extensive customization options and robust support for various EPUB formats*

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=ne0rrmatrix_EpubReader&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=ne0rrmatrix_EpubReader)
[![.NET MAUI](https://img.shields.io/badge/.NET%20MAUI-Cross%20Platform-blue)](https://dotnet.microsoft.com/en-us/apps/maui)
[![License](https://img.shields.io/github/license/ne0rrmatrix/EpubReader)](LICENSE.txt)
[![GitHub stars](https://img.shields.io/github/stars/ne0rrmatrix/EpubReader)](https://github.com/ne0rrmatrix/EpubReader/stargazers)
[![GitHub issues](https://img.shields.io/github/issues/ne0rrmatrix/EpubReader)](https://github.com/ne0rrmatrix/EpubReader/issues)

</div>

---

## 📋 Table of Contents

- [✨ Features](#-features)
- [🚀 Get Started](#-get-started)
- [🛠️ Building from Source](#️-building-from-source)
  - [PowerShell Build Scripts](#powershell-build-scripts)
- [🔥 Firebase Setup](#-firebase-setup)
- [📸 Screenshots](#-screenshots)
- [🛠️ Development](#️-development)
- [🤝 Contributing](#-contributing)
- [📄 License](#-license)

---

## ✨ Features

### 🌐 Cross-Platform Compatibility
Available on **Windows**, **Android**, **iOS**, and **macOS** with native performance on each platform.

### 📖 Reading Customization
- **📑 Chapter & Page Navigation:** Navigate seamlessly through chapters and pages
- **🎨 Font Customization:** Adjust font family and font size to your preference
- **📍 Reading Position:** Automatic tracking of reading position when switching between books
- **🎨 Color Themes:** Various color themes with automatic dark/light mode switching
- **📊 Library Sorting:** Sort by title, author, or date (on Calibre Server)

### ☁️ Cloud Sync & Authentication
- **🔐 Google Authentication:** Sign in with your Google account for cloud features
- **📱 Cross-Device Sync:** Reading progress and settings sync across devices in real-time
- **🔄 Automatic Backup:** Reading progress is automatically backed up to Firebase
- **📍 Position Tracking:** Resume reading exactly where you left off on any device
- **🔁 Offline Queueing:** Progress updates queue locally and reconcile on reconnect
- **🔒 Local-Only Mode:** Use the app without authentication for complete privacy

- **🔐 Privacy:** An in-app privacy policy is available under Settings → Privacy Policy. Cloud sync is optional and must be enabled by the user.

### 🔗 Calibre Integration
- **🔍 Discovery:** Connect to Calibre Server using **Bonjour** auto-discovery
- **🌐 Manual Connection:** Set Calibre Server URL directly
- **📚 Library Access:** Navigate your entire Calibre library
- **⬇️ Download:** Download EPUB books directly from your server

### 📚 EPUB Support
- **📖 Format Support:** Full support for **EPUB 2** and **EPUB 3.x** versions
- **🔧 Compatibility:** Enhanced compatibility for out-of-spec and broken EPUBs
- **🎯 Reliability:** Most ebooks work seamlessly (minor issues only with extremely malformed files)

### 🎨 Rich Content Display
- **🖼️ Images:** Full support for embedded images
- **💄 CSS Styling:** Complete CSS styles support within books
- **🔤 Fonts:** Full support for fonts embedded within ebooks

### 🔊 Media Overlays (EPUB 3)
- **Audio Playback:** Plays synchronized audio tracks embedded via MediaOverlays
- **SMIL Highlighting:** Highlights text in sync with audio using SMIL files
- **Read-Along Support:** Provides a seamless read-along experience with word/phrase highlighting
- **Granular Controls:** Play/pause and navigation work across chapters with overlays

---

## 🚀 Get Started

<div align="center">

### Download Now

| Platform | Download Link |
|----------|---------------|
| **Android** | [<img src="assets/android.png" />](https://play.google.com/store/apps/details?id=com.companyname.epubreader) |
| **Windows** | [<img src="assets/windows.png" />](https://apps.microsoft.com/detail/9n3t9qnkk7vx?hl=en-GB&gl=CA) |


</div>

---

## 🛠️ Building from Source

### Prerequisites

Before building the project, ensure you have:

- **Visual Studio 2026** (version 17.13 or later) with .NET MAUI workload installed
- **.NET 10.0 SDK** or later
- **Platform-specific SDKs:**
  - For Android: Android SDK (API 34 or higher)
  - For Windows: Windows App SDK
  - For iOS/macOS: Xcode 16.0 or later

### Installing .NET MAUI Workload

The .NET MAUI workload is required to build cross-platform applications. Install it using one of these methods:

#### Option 1: Using Visual Studio Installer

1. Open **Visual Studio Installer**
2. Click **Modify** on your Visual Studio 2026 installation
3. Go to the **Workloads** tab
4. Check **.NET Multi-platform App UI development**
5. Under **Installation details**, ensure the following are selected:
   - .NET MAUI SDK
   - Android SDK setup (API 34+)
   - Android Emulator
   - For iOS/Mac development: Xcode integration tools
6. Click **Modify** to install

#### Option 2: Using .NET CLI

```bash
# Install .NET MAUI workload
dotnet workload install maui

# Verify installation
dotnet workload list
```

#### Additional Workload Components

Depending on your target platforms, you may need to install additional workloads:

```bash
# For Android development
dotnet workload install android

# For iOS development (macOS only)
dotnet workload install ios

# For macOS development (macOS only)
dotnet workload install maccatalyst

# For Windows development
dotnet workload install windows
```

### Platform-Specific Requirements

#### Android Development
- **Android SDK**: API level 34 (Android 14) or higher
- **Android Emulator**: Recommended for testing
- **Java Development Kit (JDK)**: Version 21 or later (automatically installed with Visual Studio)
- **Android build tools**: Installed automatically with Android SDK

To verify Android setup:
```bash
# Check Android SDK location
dotnet build EpubReader/EpubReader.csproj -t:GetAndroidSdkInfo

# List available Android emulators
adb devices
```

#### Windows Development
- **Windows 10 SDK**: Version 19041 or later (automatically installed with Visual Studio)
- **Windows App SDK**: Included with .NET MAUI workload
- **Developer Mode**: Must be enabled in Windows Settings
  - Go to **Settings > Privacy & Security > For developers**
  - Enable **Developer Mode**

#### iOS/macOS Development (macOS only)
- **Xcode**: Version 16.0 or later
- **Xcode Command Line Tools**: Installed automatically with Xcode
- **Apple Developer Account**: Required for device deployment (free account available)
- **CocoaPods**: May be required for some dependencies

To install Xcode Command Line Tools:
```bash
xcode-select --install
```

To verify iOS setup:
```bash
# Check Xcode installation
xcodebuild -version

# List available iOS simulators
xcrun simctl list devices
```

### Verifying Your Development Environment

After installing all prerequisites, verify your setup:

```bash
# Check .NET version
dotnet --version

# List installed workloads
dotnet workload list

# Restore project dependencies
dotnet restore EpubReader/EpubReader.csproj

# Verify project can build (replace target framework as needed)
dotnet build EpubReader/EpubReader.csproj -f net10.0-android
```

### Troubleshooting Installation Issues

**Common Issues:**

1. **Workload installation fails**
   ```bash
   # Clean workload cache and reinstall
   dotnet workload clean
   dotnet workload install maui
   ```

2. **Android SDK not found**
   - Set `ANDROID_HOME` environment variable to your Android SDK location
   - Default locations:
     - Windows: `%LOCALAPPDATA%\Android\Sdk`
     - macOS: `~/Library/Android/sdk`
     - Linux: `~/Android/Sdk`

3. **Missing Visual Studio components**
   - Re-run Visual Studio Installer and ensure all .NET MAUI components are selected
   - Install optional components like Android Emulator if missing

4. **iOS build fails on Windows**
   - iOS and macOS builds require a macOS machine
   - Consider using [Remote iOS Simulator for Windows](https://docs.microsoft.com/en-us/xamarin/tools/ios-simulator/) (deprecated in .NET 9+)
   - Alternative: Use a Mac for iOS/macOS development

### Clone the Repository

```bash
git clone https://github.com/ne0rrmatrix/EpubReader.git
cd EpubReader
```

### PowerShell Build Scripts

The project includes several PowerShell scripts to simplify building and running the application with Firebase configuration.

#### Main Build Script (`build.ps1`)

The main build script handles Firebase secrets without committing them to source control. It supports multiple ways to provide Firebase configuration:

**Basic Usage:**

```powershell
# Build all platforms (Debug)
.\build.ps1

# Build specific platform
.\build.ps1 -Android -Configuration Debug
.\build.ps1 -Windows -Configuration Release

# Using switches for configuration
.\build.ps1 -Android -DebugBuild
.\build.ps1 -Windows -ReleaseBuild
```

**Providing Firebase Secrets:**

1. **Via Parameters** (highest priority):
   ```powershell
   .\build.ps1 -Android -ApiKey "your-api-key" `
               -AuthDomain "your-app.firebaseapp.com" `
               -DatabaseUrl "https://your-db.firebaseio.com"
   ```

2. **Via Google Services JSON**:
   ```powershell
   .\build.ps1 -Android -GoogleJsonPath "path\to\google-services.json"
   ```

3. **Via Environment Variables**:
   ```powershell
   $env:FIREBASE_API_KEY = "your-api-key"
   $env:FIREBASE_AUTH_DOMAIN = "your-app.firebaseapp.com"
   $env:FIREBASE_DATABASE_URL = "https://your-db.firebaseio.com"
   .\build.ps1 -Android
   ```

4. **Via build-secrets Folder** (auto-detected):
   - Place `google-services.json` in any of these locations:
     - `build-secrets/google-services.json`
     - `build-secrets/android/google-services.json`
     - `EpubReader/build-secrets/google-services.json`
     - `EpubReader/build-secrets/android/google-services.json`
   - Run: `.\build.ps1 -Android`

**Important:** Secrets are never printed to console or written to committed C# files.

#### Build with .env File (`build-from-env.ps1`)

Load Firebase secrets from `.env` or `.env.local` files:

**Setup:**

1. Create a `.env` file in the repository root:
   ```ini
   FIREBASE_API_KEY=your-api-key
   FIREBASE_AUTH_DOMAIN=your-app.firebaseapp.com
   FIREBASE_DATABASE_URL=https://your-db.firebaseio.com
   ```

2. (Optional) Create `.env.local` for local overrides (this file is gitignored)

3. Run the build:
   ```powershell
   .\.vscode\build-from-env.ps1
   ```

**Priority Order:**
- `.env.local` overrides `.env`
- Environment variables already set take precedence

#### Run Application (`run-app.ps1`)

Launch the Windows debug build after building:

```powershell
# Build and run
.\build.ps1 -Windows -DebugBuild
.\.vscode\run-app.ps1
```

This script looks for the executable at:
```
EpubReader\bin\Debug\net10.0-windows10.0.19041.0\win-x64\EpubReader.exe
```

#### Generate Android Strings (`gen-android-strings.ps1`)

Convert `google-services.json` to Android XML resource strings:

**Usage:**

```powershell
# Default paths
.\scripts\gen-android-strings.ps1

# Custom paths
.\scripts\gen-android-strings.ps1 `
  -GoogleServicesJson "path\to\google-services.json" `
  -OutFile "path\to\output\strings.secrets.xml"
```

**What it does:**
- Reads `google-services.json`
- Extracts Firebase configuration values
- Generates `strings.secrets.xml` with proper XML escaping
- Places file in `build-secrets/android/` by default

**Generated format:**
```xml
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <string name="google_api_key">your-api-key</string>
    <string name="firebase_auth_domain">your-app.firebaseapp.com</string>
    <string name="firebase_database_url">https://your-db.firebaseio.com</string>
    <string name="google_app_id">your-app-id</string>
    <string name="default_web_client_id">your-web-client-id</string>
</resources>
```

#### Security Best Practices for Scripts

1. **Never commit secrets**:
   - Add `build-secrets/` to `.gitignore`
   - Add `.env.local` to `.gitignore`
   - Use environment variables in CI/CD

2. **Use .env.local for sensitive data**:
   ```powershell
   # Create local secrets file (gitignored)
   Copy-Item .env .env.local
   # Edit .env.local with your actual secrets
   ```

3. **Verify secrets aren't leaked**:
   ```powershell
   # Check what's staged for commit
   git status
   git diff --cached
   ```

4. **For team development**:
   - Share `.env.example` template with placeholder values
   - Each developer creates their own `.env.local`
   - Use Azure Key Vault or similar for CI/CD secrets

#### Setting Up Your Developer Google Services File

For local development, you need to set up your Firebase credentials. Here's the recommended approach:

**Step 1: Download Your Configuration File**

1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Select your project
3. Go to Project Settings (gear icon)
4. Under "Your apps", find your Android app
5. Click "Download google-services.json"

**Step 2: Create build-secrets Directory**

```powershell
# Create the directory structure (already gitignored)
New-Item -ItemType Directory -Path "build-secrets" -Force
New-Item -ItemType Directory -Path "build-secrets/android" -Force
```

**Step 3: Place Configuration File**

```powershell
# Copy your downloaded file to build-secrets
Copy-Item "Downloads/google-services.json" "build-secrets/google-services.json"

# Or copy to android-specific folder
Copy-Item "Downloads/google-services.json" "build-secrets/android/google-services.json"
```

**Step 4: Verify File Placement**

```powershell
# Check file exists and is valid JSON
Test-Path "build-secrets/google-services.json"
Get-Content "build-secrets/google-services.json" | ConvertFrom-Json
```

**Step 5: Build with Your Credentials**

```powershell
# Build will automatically detect and use the file
.\build.ps1 -Android -DebugBuild

# Or explicitly specify the path
.\build.ps1 -Android -GoogleJsonPath "build-secrets/google-services.json"
```

**File Structure Reference:**

```
EpubReader/
├── build-secrets/              # Your local credentials (gitignored)
│   ├── google-services.json    # Android Firebase config
│   ├── android/
│   │   └── google-services.json
│   └── ios/
│       └── GoogleService-Info.plist
├── .env.local                  # Environment variables (gitignored)
├── build.ps1                   # Main build script
└── EpubReader/
    └── Platforms/
        ├── Android/
        │   └── google-services.json  # Optional: for direct builds
        └── iOS/
            └── GoogleService-Info.plist
```

**What's in google-services.json?**

The file contains essential Firebase configuration:
- **project_info**: Firebase project ID and database URL
- **client_info**: Your app's package name and app ID
- **api_key**: API key for Firebase services
- **oauth_client**: Web client ID for Google Sign-In

**Important Security Notes:**

⚠️ **NEVER commit this file to Git!**
- `build-secrets/` is already in `.gitignore`
- Double-check before committing: `git status`
- If accidentally committed, revoke keys in Firebase Console

✅ **Safe practices:**
- Keep files in `build-secrets/` directory
- Use different projects for dev/staging/production
- Rotate keys if ever exposed
- Use environment variables in CI/CD pipelines

**Alternative: Using Environment Variables**

Instead of files, you can extract and set environment variables:

```powershell
# Extract from google-services.json and set environment variables
$json = Get-Content "build-secrets/google-services.json" | ConvertFrom-Json
$env:FIREBASE_API_KEY = $json.client[0].api_key[0].current_key
$env:FIREBASE_AUTH_DOMAIN = "$($json.project_info.project_id).firebaseapp.com"
$env:FIREBASE_DATABASE_URL = $json.project_info.firebase_url

# Then build
.\build.ps1 -Android
```

**For Team Development:**

Create a `.env.example` template for your team:

```ini
# .env.example - Template for team members
FIREBASE_API_KEY=get-from-firebase-console
FIREBASE_AUTH_DOMAIN=your-app.firebaseapp.com
FIREBASE_DATABASE_URL=https://your-project.firebaseio.com
```

Each developer copies this to `.env.local` and fills in their values:

```powershell
Copy-Item .env.example .env.local
# Edit .env.local with your actual values
notepad .env.local
```

---

## 🔥 Firebase Setup

EpubReader uses Firebase for authentication and real-time synchronization of reading progress across devices. Follow these steps to configure Firebase for your build:

### Important: App Identification and Naming

**Default Configuration:**
- **Android Package Name**: `com.companyname.epubreader`
- **iOS Bundle ID**: `com.companyname.epubreader`
- **App Display Name**: "EpubReader"

**For Your Own Firebase Project:**

If you want to use your own Firebase project with custom naming, you'll need to:

1. **Choose Your Package Name/Bundle ID** (should be unique):
   - Format: `com.yourcompany.yourappname`
   - Example: `com.mycompany.bookviewer`
   - **Must be lowercase, no spaces or special characters except dots**

2. **Update Project Files** (if changing from default):
   - **Android**: Edit `EpubReader/Platforms/Android/AndroidManifest.xml`
     ```xml
     <manifest xmlns:android="http://schemas.android.com/apk/res/android" 
               package="com.yourcompany.yourappname">
     ```
   - **iOS**: Edit `EpubReader/Platforms/iOS/Info.plist`
     ```xml
     <key>CFBundleIdentifier</key>
     <string>com.yourcompany.yourappname</string>
     ```
   - **Project File**: Edit `EpubReader/EpubReader.csproj`
     ```xml
     <ApplicationId>com.yourcompany.yourappname</ApplicationId>
     <ApplicationTitle>Your App Name</ApplicationTitle>
     <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
     ```

3. **Match in Firebase Console**:
   - Use the **exact same package name/bundle ID** when registering your app in Firebase
   - Case-sensitive matching is required

**Quick Start with Default Settings:**

If you want to quickly test with the default settings:
- Use package name: `com.companyname.epubreader`
- Download `google-services.json` configured for this package name
- No code changes needed - everything works out of the box

### Step 1: Create a Firebase Project

1. Go to the [Firebase Console](https://console.firebase.google.com/)
2. Click **"Add project"** or select an existing project
3. Enter a project name (e.g., "EpubReader" or "Your App Name")
   - **Note**: Project name is for your reference only, doesn't affect app configuration
4. Enable Google Analytics if desired
5. Click **"Create project"**

### Step 2: Register Your Apps

#### For Android:

1. In Firebase Console, click the **Android icon** to add an Android app
2. **Enter your Android package name**:
   - **Using default**: `com.companyname.epubreader`
   - **Using custom**: `com.yourcompany.yourappname` (must match AndroidManifest.xml)
   - ⚠️ **Important**: This cannot be changed later without creating a new app in Firebase
3. Enter an app nickname (optional): "EpubReader Android" or your preferred name
   - This is just for display in Firebase Console
4. Enter your debug signing certificate SHA-1 (for development)
   - To get SHA-1, run:
   ```bash
   keytool -list -v -keystore ~/.android/debug.keystore -alias androiddebugkey -storepass android -keypass android
   ```
   - **Windows users**: Use `%USERPROFILE%\.android\debug.keystore` instead of `~/.android/`
5. Click **"Register app"**
6. Download the `google-services.json` file
7. **Verify the package name in the downloaded file**:
   ```powershell
   # Check the package name matches your app
   $json = Get-Content "Downloads/google-services.json" | ConvertFrom-Json
   $packageName = $json.client[0].client_info.android_client_info.package_name
   Write-Host "Package name in google-services.json: $packageName"
   ```
8. **Place the file in one of these locations** (for automatic detection by build scripts):
   - **Recommended**: `build-secrets/google-services.json` (project root)
   - Alternative: `build-secrets/android/google-services.json`
   - Alternative: `EpubReader/build-secrets/google-services.json`
   - Alternative: `EpubReader/Platforms/Android/google-services.json` (for direct builds)

**Package Name Troubleshooting:**
- If you see "Package name mismatch" errors, ensure:
  - AndroidManifest.xml `package` attribute matches Firebase
  - EpubReader.csproj `<ApplicationId>` matches Firebase
  - google-services.json was downloaded for the correct package name
- Use this command to verify all locations match:
  ```powershell
  # Check AndroidManifest.xml
  Select-String -Path "EpubReader/Platforms/Android/AndroidManifest.xml" -Pattern 'package="([^"]*)"'
  
  # Check google-services.json
  $json = Get-Content "build-secrets/google-services.json" | ConvertFrom-Json
  $json.client[0].client_info.android_client_info.package_name
  ```

**Important Notes:**
- The `build-secrets/` directory is gitignored to prevent committing your credentials
- The build scripts will automatically detect and use files in `build-secrets/`
- For manual builds without scripts, place in `EpubReader/Platforms/Android/`
- Ensure the file is named exactly `google-services.json` (case-sensitive)

#### For iOS:

1. In Firebase Console, click the **iOS icon** to add an iOS app
2. **Enter your iOS bundle ID**:
   - **Using default**: `com.companyname.epubreader`
   - **Using custom**: `com.yourcompany.yourappname` (must match Info.plist CFBundleIdentifier)
   - ⚠️ **Important**: This cannot be changed later without creating a new app in Firebase
3. Enter an app nickname (optional): "EpubReader iOS" or your preferred name
4. Click **"Register app"**
5. Download the `GoogleService-Info.plist` file
6. **Verify the bundle ID in the downloaded file**:
   ```bash
   # Check the bundle ID matches your app (macOS)
   /usr/libexec/PlistBuddy -c "Print :BUNDLE_ID" Downloads/GoogleService-Info.plist
   ```
7. **Place the file in one of these locations**:
   - **Recommended**: `build-secrets/ios/GoogleService-Info.plist`
   - Alternative: `EpubReader/Platforms/iOS/GoogleService-Info.plist` (for direct builds)

**Bundle ID Troubleshooting:**
- If you see bundle ID errors, ensure:
  - Info.plist `CFBundleIdentifier` matches Firebase
  - EpubReader.csproj `<ApplicationId>` matches Firebase
  - GoogleService-Info.plist was downloaded for the correct bundle ID

### Step 7: Build Configuration

After setting up Firebase:

1. **For Debug builds**: Firebase will use the downloaded config files automatically
2. **For Release builds**: Ensure config files are included or environment variables are set
3. **CI/CD**: Store Firebase config as secrets and inject during build

### Customizing the App for Your Own Use

If you want to publish your own version of EpubReader or customize it for your organization:

#### Step 1: Choose Your App Identity

```xml
<!-- Example custom values -->
Package Name: com.mycompany.ebookreader
App Name: "MyBook Reader"
Display Version: 1.0.0
```

#### Step 2: Update Android Configuration

**File: `EpubReader/Platforms/Android/AndroidManifest.xml`**
```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android"
          package="com.mycompany.ebookreader">  <!-- Change this -->
    <application 
        android:label="MyBook Reader"            <!-- Change this -->
        android:icon="@mipmap/appicon">
        <!-- ... rest of manifest ... -->
    </application>
</manifest>
```

#### Step 3: Update iOS Configuration

**File: `EpubReader/Platforms/iOS/Info.plist`**
```xml
<dict>
    <key>CFBundleIdentifier</key>
    <string>com.mycompany.ebookreader</string>  <!-- Change this -->
    
    <key>CFBundleDisplayName</key>
    <string>MyBook Reader</string>              <!-- Change this -->
    
    <key>CFBundleName</key>
    <string>MyBook Reader</string>              <!-- Change this -->
    
    <!-- ... rest of plist ... -->
</dict>
```

#### Step 4: Update Project File

**File: `EpubReader/EpubReader.csproj`**
```xml
<PropertyGroup>
    <ApplicationId>com.mycompany.ebookreader</ApplicationId>  <!-- Change this -->
    <ApplicationTitle>MyBook Reader</ApplicationTitle>         <!-- Change this -->
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion> <!-- Your version -->
    <ApplicationVersion>1</ApplicationVersion>
    
    <!-- Optional: Customize icons -->
    <ApplicationIcon>Resources\AppIcon\appicon.svg</ApplicationIcon>
</PropertyGroup>
```

#### Step 5: Register Custom App in Firebase

1. In Firebase Console, register your app with your custom package name/bundle ID
2. Download new `google-services.json` and `GoogleService-Info.plist`
3. Place them in `build-secrets/` directory
4. Verify package names match:

```powershell
# Verify Android
$json = Get-Content "build-secrets/google-services.json" | ConvertFrom-Json
Write-Host "Firebase Package: $($json.client[0].client_info.android_client_info.package_name)"
$manifest = Get-Content "EpubReader/Platforms/Android/AndroidManifest.xml" -Raw
if ($manifest -match 'package="([^"]*)"') {
    Write-Host "Manifest Package: $($Matches[1])"
}

# Verify iOS (macOS only)
$plist = /usr/libexec/PlistBuddy -c "Print :BUNDLE_ID" build-secrets/ios/GoogleService-Info.plist
Write-Host "Firebase Bundle ID: $plist"
```

#### Step 6: Update SHA-1 Certificates

For Android, add your debug and release certificates to Firebase:

```bash
# Debug certificate
keytool -list -v -keystore ~/.android/debug.keystore -alias androiddebugkey -storepass android -keypass android

# Release certificate (if you have one)
keytool -list -v -keystore /path/to/your-release.keystore -alias your-alias
```

Copy the SHA-1 fingerprints and add them in Firebase Console:
- Project Settings → Your Android App → Add Fingerprint

#### Step 7: Test Your Configuration

```powershell
# Build with your custom configuration
.\build.ps1 -Android -GoogleJsonPath "build-secrets/google-services.json"

# Verify the app installs with your custom package name
adb shell pm list packages | Select-String "com.mycompany"
```

#### Common Customization Checklist

- [ ] Updated package name in AndroidManifest.xml
- [ ] Updated bundle ID in Info.plist
- [ ] Updated ApplicationId in EpubReader.csproj
- [ ] Updated ApplicationTitle in EpubReader.csproj
- [ ] Created Firebase project with custom app registrations
- [ ] Downloaded google-services.json with matching package name
- [ ] Downloaded GoogleService-Info.plist with matching bundle ID
- [ ] Added SHA-1 certificates to Firebase Console
- [ ] Verified package names match across all files
- [ ] Tested build and run with custom configuration
- [ ] (Optional) Updated app icons and splash screens
- [ ] (Optional) Updated app display name in XAML files

#### Using Default Configuration

**If you just want to use the app as-is for testing:**

1. Keep default package name: `com.companyname.epubreader`
2. Create Firebase project and register with default names
3. Download config files for `com.companyname.epubreader`
4. Place in `build-secrets/` directory
5. Build and run - no code changes needed!

**Advantages of keeping defaults:**
- Faster setup for testing
- No risk of typos in multiple files
- Easy to follow tutorials and documentation
- Can change later when ready to publish

**When to customize:**
- Publishing to Google Play Store or Apple App Store
- Creating a fork for your organization
- Adding custom branding
- Deploying to production

### Testing Firebase Integration

1. Build and run the app
2. Navigate to the login page
3. Sign in with a Google account
4. Open a book and read a few pages
5. Sign in on another device with the same account
6. Verify that reading progress syncs automatically

### Local-Only Mode

Users can also use the app without Firebase authentication:
- On the login page, select "Continue without signing in"
- All data will be stored locally only
- Cloud sync features will be disabled

### Troubleshooting Firebase Setup

#### Common Build Errors

**1. "google-services.json not found" or "Missing google-services.json"**

**Cause**: The build process cannot locate your Firebase configuration file.

**Solutions**:
- Verify the file exists in one of these locations:
  ```
  build-secrets/google-services.json
  build-secrets/android/google-services.json
  EpubReader/Platforms/Android/google-services.json
  ```
- Check the filename is exactly `google-services.json` (case-sensitive, no extra extensions)
- Ensure you downloaded the Android version (not iOS version)
- If using PowerShell scripts, check environment variables are set:
  ```powershell
  $env:FIREBASE_API_KEY
  $env:FIREBASE_AUTH_DOMAIN
  $env:FIREBASE_DATABASE_URL
  ```

**2. "FirebaseApp with name [DEFAULT] doesn't exist"**

**Cause**: Firebase configuration values are missing or incorrect.

**Solutions**:
- Re-download `google-services.json` from Firebase Console
- Verify the file contains valid JSON (open in text editor)
- Check that the package name matches: `com.companyname.epubreader`
- If using environment variables, ensure all required variables are set:
  ```bash
  # Check variables are set (values will be hidden)
  echo $env:FIREBASE_API_KEY -ne $null
  echo $env:FIREBASE_AUTH_DOMAIN -ne $null
  echo $env:FIREBASE_DATABASE_URL -ne $null
  ```
- Clean and rebuild:
  ```bash
  dotnet clean
  dotnet build -f net10.0-android
  ```

**3. "Failed to parse google-services.json"**

**Cause**: The JSON file is corrupted or has invalid formatting.

**Solutions**:
- Re-download the file from Firebase Console
- Open the file in a text editor and verify it's valid JSON
- Check for extra characters or byte-order marks (BOM) at the start
- Use a JSON validator online to check structure
- Ensure the file wasn't modified during download (check file size)

**4. "Package name mismatch" or "SHA-1 certificate fingerprint mismatch"**

**Cause**: The app's package name or signing certificate doesn't match Firebase configuration.

**Solutions**:
- Verify package name in Firebase Console matches your app
- For debug builds, add debug keystore SHA-1 to Firebase Console:
  ```bash
  keytool -list -v -keystore ~/.android/debug.keystore -alias androiddebugkey -storepass android -keypass android
  ```
- For release builds, add your release keystore SHA-1
- In Firebase Console, go to Project Settings → Your Android App → Add Fingerprint

**5. "FIREBASE_API_KEY environment variable not set"**

**Cause**: Build script requires Firebase secrets but they weren't provided.

**Solutions**:
- Use PowerShell build script with secrets:
  ```powershell
  .\build.ps1 -Android -GoogleJsonPath "path\to\google-services.json"
  ```
- Or set environment variables:
  ```powershell
  $env:FIREBASE_API_KEY = "your-key-here"
  $env:FIREBASE_AUTH_DOMAIN = "your-app.firebaseapp.com"
  $env:FIREBASE_DATABASE_URL = "https://your-db.firebaseio.com"
  ```
- Or place `google-services.json` in `build-secrets/` folder

#### Common Runtime Errors

**1. "Google Sign-In failed" or "Error code: 10"**

**Cause**: Google authentication is not properly configured or credentials are incorrect.

**Solutions**:
- Verify Google Sign-In is enabled in Firebase Console:
  - Go to Authentication → Sign-in method
  - Ensure Google provider is enabled
- Check SHA-1 fingerprint is added in Firebase Console
- Ensure `default_web_client_id` is correct in `google-services.json`
- For Android, verify the OAuth client ID is configured:
  - Go to [Google Cloud Console](https://console.cloud.google.com)
  - APIs & Services → Credentials
  - Ensure Android OAuth client exists with correct package name and SHA-1

**2. "Permission denied" when accessing Firebase Realtime Database**

**Cause**: Firebase security rules are too restrictive or user is not authenticated.

**Solutions**:
- Check Firebase Realtime Database security rules
- For development, temporarily use test mode rules:
  ```json
  {
    "rules": {
      ".read": "auth != null",
      ".write": "auth != null"
    }
  }
  ```
- Ensure user is successfully signed in before accessing database
- Check Firebase Console → Realtime Database → Rules tab
- Verify database URL matches in configuration

**3. "FirebaseException: Invalid API key"**

**Cause**: The API key in your configuration is incorrect or has been regenerated.

**Solutions**:
- Re-download fresh `google-services.json` from Firebase Console
- Check for typos if setting API key manually
- Verify API key restrictions in Google Cloud Console:
  - Go to APIs & Services → Credentials
  - Click on your API key
  - Check Android restrictions match your app
- Clear app data/cache and rebuild:
  ```bash
  dotnet clean
  rm -rf EpubReader/bin EpubReader/obj
  dotnet build -f net10.0-android
  ```

**4. "Network error" or "Unable to resolve host"**

**Cause**: Network connectivity issues or incorrect Firebase database URL.

**Solutions**:
- Verify device/emulator has internet connection
- Check Firebase database URL format:
  - Should be: `https://your-project-id.firebaseio.com`
  - Not: `https://your-project-id.web.app`
- Test Firebase connection in browser
- Check firewall/proxy settings aren't blocking Firebase domains
- Ensure Firebase project is active (not deleted or suspended)

**5. "App crashes immediately on launch with Firebase error"**

**Cause**: Critical Firebase configuration error or missing required fields.

**Solutions**:
- Check Android Logcat for detailed error:
  ```bash
  adb logcat | grep -i firebase
  ```
- Verify all required fields in `google-services.json`:
  - `project_info.project_id`
  - `project_info.firebase_url`
  - `client[0].client_info.mobilesdk_app_id`
  - `client[0].api_key[0].current_key`
- Ensure FirebaseConfigLoader is working correctly
- Try running in local-only mode to isolate Firebase issues
- Check Visual Studio Output window for detailed error messages

#### Debugging Firebase Issues

**Enable Detailed Logging:**

For Android, add to your debug session:
```bash
adb logcat | Select-String -Pattern "Firebase|Auth|Database"
```

**Verify Configuration at Runtime:**

Add temporary logging in your code:
```csharp
// In FirebaseConfigLoader or MauiProgram.cs
System.Diagnostics.Trace.WriteLine($"Firebase API Key set: {!string.IsNullOrEmpty(apiKey)}");
System.Diagnostics.Trace.WriteLine($"Firebase Auth Domain: {authDomain}");
System.Diagnostics.Trace.WriteLine($"Firebase Database URL: {databaseUrl}");
```

**Test Firebase Connection:**

Use Firebase Console to test:
1. Go to Authentication → Users
2. Try signing in with a test Google account
3. Go to Realtime Database → Data
4. Verify data structure and permissions

**Common Checklist:**

- [ ] `google-services.json` is in the correct location
- [ ] Package name matches: `com.companyname.epubreader`
- [ ] SHA-1 fingerprint is added to Firebase Console
- [ ] Google Sign-In is enabled in Firebase Console
- [ ] Realtime Database is created and has proper security rules
- [ ] API key is valid and not restricted incorrectly
- [ ] Database URL format is correct
- [ ] Device/emulator has internet connectivity
- [ ] Firebase project is active and billing is enabled (if required)

#### Getting Help

If you're still experiencing issues:

1. **Check build output logs** in Visual Studio Output window
2. **Review Firebase Console** for any project warnings or quota issues
3. **Test with a fresh Firebase project** to rule out account issues
4. **Try local-only mode** to verify the app works without Firebase
5. **Check GitHub Issues** for similar problems and solutions

**Useful Commands for Diagnostics:**

```powershell
# Check if file exists
Test-Path "build-secrets/google-services.json"

# Validate JSON file
Get-Content "build-secrets/google-services.json" | ConvertFrom-Json

# Check environment variables
Get-ChildItem Env: | Where-Object { $_.Name -like "*FIREBASE*" }

# Clean build artifacts
dotnet clean
Remove-Item -Recurse -Force EpubReader/bin, EpubReader/obj -ErrorAction SilentlyContinue

# Rebuild with verbose logging
dotnet build EpubReader/EpubReader.csproj -f net10.0-android -v detailed
```

---

## 📸 Screenshots

### Windows

#### 🖥️ Default Light Mode Theme

<div align="center">
<img width="480" alt="Windows Light Mode Library View" src="https://github.com/user-attachments/assets/cea99531-5f4d-476f-987e-06dabed2029a" />
<img width="480" alt="Windows Light Mode Reading View" src="https://github.com/user-attachments/assets/f87c5feb-f020-42a6-b366-223d259b929f" />
<img width="480" alt="Windows Light Mode Settings" src="https://github.com/user-attachments/assets/6d583793-ee00-4fe0-b264-30dc975784af" />
<img width="480" alt="Windows Light Mode Calibre Connection" src="https://github.com/user-attachments/assets/6c1cedf2-ad1f-4eb9-a9a4-204edc492cb1" />
<img width="480" alt="Windows Light Mode Font Settings" src="https://github.com/user-attachments/assets/e896a905-24be-409f-9825-7c7240bed7d4" />
<img width="480" alt="Windows Light Mode Color Themes" src="https://github.com/user-attachments/assets/bd311d21-2cdf-46d6-bfbf-ae876f962ec7" />
<img width="480" alt="Windows Light Mode Chapter Navigation" src="https://github.com/user-attachments/assets/1d469867-be82-43ca-bfea-01f918c63060" />
</div>

---

#### 🌙 Dark Mode Theme (Sepia)

<div align="center">
<img width="480" alt="Windows Sepia Theme Library View" src="https://github.com/user-attachments/assets/6dc6caf9-ba8e-4875-b700-64d63ae90d58" />
<img width="480" alt="Windows Sepia Theme Reading View" src="https://github.com/user-attachments/assets/3c83d016-caa4-467d-b509-ec368379baf4" />
<img width="480" alt="Windows Sepia Theme Settings" src="https://github.com/user-attachments/assets/8b6715ba-4fdf-498d-af00-ecaeaed1a6fd" />
<img width="480" alt="Windows Sepia Theme Calibre View" src="https://github.com/user-attachments/assets/28e1c8cb-e701-4baf-b514-3c2fadcfa3df" />
<img width="480" alt="Windows Sepia Theme Font Options" src="https://github.com/user-attachments/assets/de5a2963-75b4-4b9b-b7eb-d7f94c2e1b06" />
<img width="480" alt="Windows Sepia Theme Color Selection" src="https://github.com/user-attachments/assets/d1741667-44e6-4290-8f24-8930d76e7b29" />
<img width="480" alt="Windows Sepia Theme Chapter View" src="https://github.com/user-attachments/assets/3cd7d857-91dd-4dc2-8756-e269e5d357b7" />
</div>

---

### 📱 Android

#### ☀️ Light Mode (Sepia Theme)

<div align="center">
<img width="240" alt="Android Sepia Library View" src="https://github.com/user-attachments/assets/a65c5247-ea0b-4517-89f9-023cf21c0eb5" />
<img width="240" alt="Android Sepia Reading View" src="https://github.com/user-attachments/assets/68937493-a30e-4437-90fb-34a0a08d0b07" />
<img width="240" alt="Android Sepia Book Details" src="https://github.com/user-attachments/assets/ab925b21-3734-4a9b-997e-17449f38122b" />
<img width="240" alt="Android Sepia Settings" src="https://github.com/user-attachments/assets/e107d0fe-267c-41c1-9918-83d4672ef9b0" />
<img width="240" alt="Android Sepia Font Settings" src="https://github.com/user-attachments/assets/7d4610b5-5ccd-4315-b99a-f016e45fb8a8" />
<img width="240" alt="Android Sepia Color Themes" src="https://github.com/user-attachments/assets/80decdb6-3ae4-4965-a734-615e0e7db2c5" />
<img width="240" alt="Android Sepia Calibre Connection" src="https://github.com/user-attachments/assets/4a7c5895-4876-4580-9c27-5684cc510ca5" />
<img width="240" alt="Android Sepia Chapter Navigation" src="https://github.com/user-attachments/assets/7e282ce0-398e-4d1f-a48d-cbee51e7201c" />
<img width="240" alt="Android Sepia Reading Progress" src="https://github.com/user-attachments/assets/a6d92143-2f4d-4013-874f-8a72e413f2d1" />
</div>

---

#### 🌙 Dark Mode

<div align="center">
<img width="240" alt="Android Dark Mode Library View" src="https://github.com/user-attachments/assets/a06691eb-ac19-445a-9ee3-1bfa2d2a37a4" />
<img width="240" alt="Android Dark Mode Reading View" src="https://github.com/user-attachments/assets/f70c432a-5af1-4dc7-a282-e6f7e3b946ba" />
<img width="240" alt="Android Dark Mode Book Details" src="https://github.com/user-attachments/assets/6c9bde8a-de68-4e3b-a37a-655fa1e627c2" />
<img width="240" alt="Android Dark Mode Settings" src="https://github.com/user-attachments/assets/1cdcb3ae-2a4f-422d-a725-80bfc8ac6463" />
<img width="240" alt="Android Dark Mode Font Options" src="https://github.com/user-attachments/assets/8f87da66-e216-4f02-a478-727ca9c26808" />
<img width="240" alt="Android Dark Mode Color Themes" src="https://github.com/user-attachments/assets/a6495dba-f8df-45c8-b31f-76460241cc20" />
<img width="240" alt="Android Dark Mode Navigation" src="https://github.com/user-attachments/assets/240e440e-5d8f-4a58-a203-83bdb667bf03" />
</div>

---

## 🛠️ Development
