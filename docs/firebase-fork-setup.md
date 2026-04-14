# Firebase setup for developers who fork `EpubReader`

This guide is for developers who fork this repo and want to run it against **their own Firebase project**.

It is written for someone starting from zero. If you have never created a Firebase project, never downloaded `google-services.json`, and never wired a .NET MAUI app to Firebase before, follow this guide in order.

## What this guide covers

- creating your own Firebase project
- registering the Android app in Firebase
- downloading your own `google-services.json`
- placing the file where this repo expects it
- enabling the Firebase features this repo uses
- building the app with the local `build.ps1` script
- avoiding the most common setup mistakes

## Official documentation used for this guide

### Google / Firebase

- Firebase Android setup: `https://firebase.google.com/docs/android/setup`
- Firebase project setup: `https://firebase.google.com/docs/projects/learn-more`
- Firebase Google Sign-In for Android: `https://firebase.google.com/docs/auth/android/google-signin`
- Firebase Realtime Database: `https://firebase.google.com/docs/database`
- Firebase Authentication: `https://firebase.google.com/docs/auth`

### .NET MAUI / Microsoft Learn

- .NET MAUI single-project overview: `https://learn.microsoft.com/dotnet/maui/fundamentals/single-project?view=net-maui-10.0`
- .NET MAUI project configuration: `https://learn.microsoft.com/dotnet/maui/deployment/visual-studio-properties?view=net-maui-10.0`

## PowerShell command path conventions used in this guide

To make the examples copy/paste safe, this guide uses these path conventions:

- **Repository root** means the folder that contains `build.ps1`, `README.md`, `docs/`, and the `EpubReader/` project folder.
- In your clone, that will look like:
  - `C:\Users\james\source\repos\EpubReader`
- Unless a step says otherwise, run all PowerShell commands from the **repository root**.

Before running the script examples, change into your clone root:

```powershell
Set-Location "C:\path\to\your\EpubReader-clone"
```

Then commands such as these work as written:

```powershell
./build.ps1 -Android -DebugBuild
Copy-Item "build-secrets\google-services.json" "EpubReader\Resources\Raw\google-services.json" -Force
```

If you are **not** in the repository root, use the script's full path instead:

```powershell
pwsh -File "C:\path\to\your\EpubReader-clone\build.ps1" -Android -DebugBuild
```

`build.ps1` resolves repo-relative paths from the script location, so `pwsh -File <full-path-to-build.ps1>` is the safest option when you are unsure of your current directory.

## How this repo is wired

Before you create anything in Firebase, understand these repo-specific facts:

- The .NET MAUI app project is `EpubReader/EpubReader.csproj`.
- The current app ID is defined in that project file with `ApplicationId`.
- The current default Android app ID is `com.companyname.epubreader`.
- Android builds use `google-services.json`.
- The local build script is `build.ps1` in the repository root.
- The Android target in `EpubReader/EpubReader.csproj` includes this item:

```xml
<ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">
  <GoogleServicesJson Include="Resources\Raw\google-services.json" />
</ItemGroup>
```

- That means **direct Android builds from Visual Studio, Visual Studio Code, or `dotnet build` expect a real file at `EpubReader/Resources/Raw/google-services.json`**.
- The build script looks for `google-services.json` in these locations:
  - `build-secrets/google-services.json`
  - `build-secrets/android/google-services.json`
  - `EpubReader/build-secrets/google-services.json`
  - `EpubReader/build-secrets/android/google-services.json`

This leads to two supported workflows:

1. **Build script workflow**
   - keep your secret file in `build-secrets/...`
   - run `./build.ps1`
   - the script stages the file into `EpubReader/Resources/Raw/google-services.json` for the build
2. **Direct IDE workflow**
   - place your real file directly at `EpubReader/Resources/Raw/google-services.json`
   - then use Visual Studio, Visual Studio Code, or direct `dotnet build`

For a first successful build, the easiest path is:

1. keep the existing app ID
2. create a Firebase Android app for that exact app ID
3. download `google-services.json`
4. put it in `build-secrets/google-services.json`
5. run `./build.ps1 -Android -DebugBuild`

If you change the app ID later, you must register a matching app in Firebase and download a new `google-services.json`.

## Step 0: Fork and clone the repo

1. Fork the repository in GitHub.
2. Clone your fork locally.
3. Open the solution in Visual Studio 2026 with the .NET MAUI workload installed.
4. Confirm that `EpubReader/EpubReader.csproj` loads correctly.

## Step 1: Decide whether to keep the default app ID or use your own

### Option A: keep the default app ID

Use this if you just want to get the app running quickly.

Current value in this repo:

- `com.companyname.epubreader`

If you choose this option:

- do **not** change `ApplicationId` yet
- create the Android app in Firebase with exactly `com.companyname.epubreader`

### Option B: use your own app ID

Use this if you want your fork to have its own identity.

Typical format:

- `com.yourname.epubreader`
- `com.yourcompany.epubreader`
- `com.yourorg.bookreader`

If you choose this option, update `EpubReader/EpubReader.csproj` first:

```xml
<ApplicationId>com.yourcompany.epubreader</ApplicationId>
```

In .NET MAUI single-project apps, the app ID is configured in the project file. Microsoft Learn documents this under the .NET MAUI project configuration and single-project model.

Important:

- the Firebase Android app package name must match the app ID exactly
- if they do not match, Google sign-in will fail

## Step 1A: Review Android signing settings in `EpubReader.csproj`

This repo's Android `Debug` and `Release` builds also depend on the Android signing properties in `EpubReader/EpubReader.csproj`.

At the time of writing, the project contains Android-specific signing blocks for both:

- `Release|net10.0-android|AnyCPU`
- `Debug|net10.0-android|AnyCPU`

Those property groups point to:

- `AndroidSigningKeyStore`
- `AndroidSigningStorePass`
- `AndroidSigningKeyAlias`
- `AndroidSigningKeyPass`

If those values are wrong for your machine, Android builds can fail before the app runs.

### Important path rule

`AndroidSigningKeyStore` is resolved relative to `EpubReader/EpubReader.csproj`, not relative to the repository root.

So these are different:

- `Epubreader.keystore`
  - means a file next to `EpubReader.csproj`
- `..\build-secrets\android\MyFork.keystore`
  - means a file under `build-secrets/android/` at the repository root

### Shortest path

If the existing repo keystore works for your local build, you can leave these properties unchanged.

If you want to use your own signing file, update **both** Android property groups in `EpubReader/EpubReader.csproj` so that `Debug` and `Release` are consistent.

Example shape:

```xml
<PropertyGroup Condition="'$(Configuration)|$(TargetFramework)|$(Platform)'=='Release|net10.0-android|AnyCPU'">
  <AndroidPackageFormat>apk</AndroidPackageFormat>
  <AndroidKeyStore>True</AndroidKeyStore>
  <AndroidUseAapt2>True</AndroidUseAapt2>
  <AndroidCreatePackagePerAbi>False</AndroidCreatePackagePerAbi>
  <AndroidSigningKeyStore>..\build-secrets\android\MyFork.keystore</AndroidSigningKeyStore>
  <AndroidSigningStorePass>your-store-password</AndroidSigningStorePass>
  <AndroidSigningKeyAlias>your-key-alias</AndroidSigningKeyAlias>
  <AndroidSigningKeyPass>your-key-password</AndroidSigningKeyPass>
</PropertyGroup>

<PropertyGroup Condition="'$(Configuration)|$(TargetFramework)|$(Platform)'=='Debug|net10.0-android|AnyCPU'">
  <AndroidUseAapt2>True</AndroidUseAapt2>
  <AndroidCreatePackagePerAbi>False</AndroidCreatePackagePerAbi>
  <AndroidPackageFormat>apk</AndroidPackageFormat>
  <AndroidKeyStore>True</AndroidKeyStore>
  <AndroidSigningKeyStore>..\build-secrets\android\MyFork.keystore</AndroidSigningKeyStore>
  <AndroidSigningStorePass>your-store-password</AndroidSigningStorePass>
  <AndroidSigningKeyAlias>your-key-alias</AndroidSigningKeyAlias>
  <AndroidSigningKeyPass>your-key-password</AndroidSigningKeyPass>
</PropertyGroup>
```

### When you should update these values

Update the Android signing values if:

- your fork uses a different keystore file
- the keystore file is stored in a different folder
- the alias or passwords are different on your machine
- Android build fails with signing or keystore errors

### Recommended local layout for a fork

To keep custom signing files out of source control, a practical local layout is:

- `build-secrets/android/MyFork.keystore`

Then set:

- `AndroidSigningKeyStore` to `..\build-secrets\android\MyFork.keystore`

Because that path is relative to `EpubReader/EpubReader.csproj`.

### After editing the signing block

From the repository root, test the Android build again:

```powershell
./build.ps1 -Android -DebugBuild
```

or, from any directory:

```powershell
pwsh -File "C:\path\to\your\EpubReader-clone\build.ps1" -Android -DebugBuild
```

## Step 2: Create your Firebase project

1. Go to the Firebase Console:
   - `https://console.firebase.google.com/`
2. Select **Create a project**.
3. Enter a project name.
   - Example: `EpubReader-MyFork`
4. Choose whether to enable Google Analytics.
   - For local development, this is optional.
5. Finish creating the project.

After the project is created, you will be on the Firebase project dashboard.

## Step 3: Register the Android app in Firebase

1. In your Firebase project, click **Add app**.
2. Choose **Android**.
3. Enter the Android package name.
   - If you kept the default repo value, use `com.companyname.epubreader`.
   - If you changed `ApplicationId`, use your new exact value.
4. Enter an app nickname if you want.
   - Example: `EpubReader Android Dev`
5. Enter your debug SHA-1 certificate fingerprint.
   - This is strongly recommended for Google Sign-In.

### How to get the debug SHA-1 on Windows

Open PowerShell and run:

```powershell
keytool -list -v -keystore "$env:USERPROFILE\.android\debug.keystore" -alias androiddebugkey -storepass android -keypass android
```

Look for:

- `SHA1:`

Copy that SHA-1 value and paste it into Firebase when registering the Android app.

If `keytool` is not found:

- install the Android / Java tooling that comes with the MAUI and Android workload in Visual Studio
- or run the command from a developer shell where Java is on `PATH`

6. Click **Register app**.

## Step 4: Download your own `google-services.json`

After registering the Android app, Firebase offers a configuration file download.

1. Click **Download `google-services.json`**.
2. Save it somewhere temporary, such as your Downloads folder.
3. Do **not** reuse the file from this repo for your fork.
4. Do **not** rename it.

The filename must stay exactly:

- `google-services.json`

## Step 5: Verify the file matches your app

Before placing the file in the repo, verify it belongs to your Firebase project and your app ID.

In PowerShell:

```powershell
$json = Get-Content "$env:USERPROFILE\Downloads\google-services.json" -Raw | ConvertFrom-Json
$json.project_info.project_id
$json.client[0].client_info.android_client_info.package_name
$json.client[0].oauth_client | Select-Object client_type, client_id
```

What to check:

- `project_id` is your Firebase project
- `package_name` matches your `.NET MAUI` `ApplicationId`
- there is an OAuth client with `client_type` `3`
  - this is the web client ID used by Google sign-in

If `package_name` does not match your app ID, stop and register the app again in Firebase with the correct package name.

## Step 6: Place `google-services.json` in the repo

Recommended location for the `build.ps1` workflow:

- `build-secrets/google-services.json`

Create the folder if needed:

```powershell
New-Item -ItemType Directory -Path "build-secrets" -Force
Copy-Item "$env:USERPROFILE\Downloads\google-services.json" "build-secrets\google-services.json" -Force
```

You can also use:

- `build-secrets/android/google-services.json`

The repo build script will detect either location.

### Important: direct IDE builds use `EpubReader/Resources/Raw/google-services.json`

Because the Android target includes `GoogleServicesJson Include="Resources\Raw\google-services.json"`, direct Android builds expect the file to exist inside the project at:

- `EpubReader/Resources/Raw/google-services.json`

If you want to build or debug without `build.ps1`, copy the file there:

```powershell
Copy-Item "build-secrets\google-services.json" "EpubReader\Resources\Raw\google-services.json" -Force
```

Use this direct-project copy when you want to:

- press **F5** in Visual Studio
- press **F5** in Visual Studio Code
- run `dotnet build` manually
- use other IDE tasks that do not call `build.ps1`

Important:

- keep this file local
- do not commit your personal Firebase config to your fork unless you intentionally want it public
- always check `git status` before committing

## Step 7: Enable Firebase Authentication with Google provider

This repo uses Google sign-in on Android.

In Firebase Console:

1. Open **Authentication**.
2. Open **Sign-in method**.
3. Enable **Google**.
4. Choose or verify the project support email.
5. Save.

If Google provider is disabled, the login flow will start but Firebase sign-in will fail.

## Step 8: Enable Realtime Database

This repo also uses Firebase Realtime Database for sync.

In Firebase Console:

1. Open **Build** → **Realtime Database**.
2. Click **Create Database**.
3. Choose a region.
4. Choose your starting security mode.

For early local development, many developers start with test mode temporarily and then tighten rules.

After the database is created, verify that your downloaded `google-services.json` has a `firebase_url` entry.

You can check with:

```powershell
$json = Get-Content "build-secrets\google-services.json" -Raw | ConvertFrom-Json
$json.project_info.firebase_url
```

## Step 9: Build the app with your Firebase config

From the repository root, run one of these commands.

### Simplest Android build

```powershell
./build.ps1 -Android -DebugBuild
```

Run that command from the repository root.

### Android build with explicit file path

```powershell
./build.ps1 -Android -DebugBuild -GoogleJsonPath "./build-secrets/google-services.json"
```

That relative `./build-secrets/...` path assumes you are running the command from the repository root.

### Build using explicit Firebase values

```powershell
./build.ps1 -Android -DebugBuild -ApiKey "your-api-key" -AuthDomain "your-project.firebaseapp.com" -DatabaseUrl "https://your-project-default-rtdb.firebaseio.com"
```

If you want to run the script from another directory, prefer:

```powershell
pwsh -File "C:\path\to\your\EpubReader-clone\build.ps1" -Android -DebugBuild
```

What the build script does:

- sets Firebase environment variables for the build process
- looks for your `google-services.json`
- stages it into the Android resource location when needed
- builds `EpubReader/EpubReader.csproj`

## Step 10: Build and run from Visual Studio or Visual Studio Code

If you use an IDE instead of `build.ps1`, make sure your Firebase file is in the project first:

- `EpubReader/Resources/Raw/google-services.json`

### Windows-specific note

This repo's `FirebaseConfig` currently reads Firebase settings from the packaged `google-services.json` file at runtime.

That means the Windows app also needs the file packaged into the app, which is why for Windows builds you should copy your file to:

- `EpubReader/Resources/Raw/google-services.json`

Do not assume that passing only `-ApiKey`, `-AuthDomain`, or `-DatabaseUrl` is enough for the Windows app today. For the current codebase, the safest Windows setup is to provide a real `google-services.json` file in `Resources/Raw` before building or running.

### Windows workflow

If you want to run the Windows version of the app with your own Firebase project:

1. Copy your Firebase file into the project:

```powershell
Copy-Item "build-secrets\google-services.json" "EpubReader\Resources\Raw\google-services.json" -Force
```

Run that command from the repository root.

2. Build or run the Windows target.

Examples:

```powershell
./build.ps1 -Windows -DebugBuild
dotnet build EpubReader/EpubReader.csproj -f net10.0-windows10.0.19041.0
```

Run both commands from the repository root.

3. Launch the Windows app from Visual Studio, Visual Studio Code, or your existing Windows run task/script.

### Visual Studio 2026 workflow

Microsoft Learn documents that .NET MAUI Android apps can be run from Visual Studio by choosing an Android emulator in the debug target and starting the app.

1. Open the repo in Visual Studio 2026.
2. Confirm the `.NET MAUI` workload is installed.
3. Confirm your Firebase file exists at:
   - `EpubReader/Resources/Raw/google-services.json`
4. In Solution Explorer, select the `EpubReader` project.
5. In the Visual Studio toolbar, choose an Android emulator or connected device as the debug target.
6. Press **F5** or click **Start**.

If Visual Studio prompts you to install missing Android components, let it complete that setup first. Microsoft Learn documents this emulator and SDK flow in the .NET MAUI first-app guidance.

### Visual Studio 2026 Windows workflow

1. Open the repo in Visual Studio 2026.
2. Confirm your Firebase file exists at:
   - `EpubReader/Resources/Raw/google-services.json`
3. In the Visual Studio toolbar, choose **Windows Machine** as the debug target.
4. Press **F5** or click **Start**.

If the app starts but fails during Firebase initialization, re-check that `google-services.json` was copied into `EpubReader/Resources/Raw` before the build.

### Visual Studio Code workflow

Microsoft Learn documents that .NET MAUI projects in Visual Studio Code use the `.NET MAUI` extension, the `.NET MAUI: Configure Android` command, and an Android debug target selected from the status bar.

1. Install the `.NET MAUI` extension in Visual Studio Code.
2. Open the repo folder.
3. Press `Ctrl+Shift+P` and run:
   - ` .NET MAUI: Configure Android`
   - then `Refresh Android environment`
4. Confirm your Firebase file exists at:
   - `EpubReader/Resources/Raw/google-services.json`
5. In the status bar, choose an Android emulator or connected device as the debug target.
6. Press **F5** or use the **Run** button.

If Visual Studio Code reports missing Android SDK or JDK components, use the official `.NET MAUI: Configure Android` flow to install or point to them before trying again.

### Visual Studio Code Windows workflow

1. Open the repo folder in Visual Studio Code.
2. Confirm your Firebase file exists at:
   - `EpubReader/Resources/Raw/google-services.json`
3. In the status bar, choose **Windows** as the debug target.
4. Press **F5** or use the **Run** button.

If you use a VS Code task or manual CLI command for Windows, copy the Firebase file into `EpubReader/Resources/Raw` first so it is packaged with the app.

## Step 11: Run and verify Google sign-in

After the app launches on Android:

1. navigate to the login screen
2. tap Google sign-in
3. choose a Google account
4. confirm sign-in completes
5. read a book and verify the app continues working

If sign-in fails, check the troubleshooting section below.

## Step 12: If you changed the app ID, keep Firebase and MAUI in sync

If you want your fork to use a custom app ID, keep these aligned:

- `EpubReader/EpubReader.csproj`
  - `<ApplicationId>`
- Firebase Android app registration
  - Android package name
- downloaded `google-services.json`
  - `client[0].client_info.android_client_info.package_name`

When you change the app ID:

1. update `ApplicationId`
2. register a new Android app in Firebase with the new package name
3. download a new `google-services.json`
4. replace your local file in `build-secrets`
5. rebuild

## Optional: iOS and Mac Catalyst notes

Be aware of the current repo state:

- Android Google sign-in is wired
- `AuthenticationService.macios.cs` currently blocks Google sign-in until native iOS / Mac Catalyst provider configuration is completed

If you only want a successful first-time setup for your fork, start with Android.

If you later want iOS support, you will also need:

- an iOS app registered in Firebase
- a `GoogleService-Info.plist`
- Apple signing / provisioning configured for your bundle ID
- native iOS Google sign-in configuration completed in the app

## Troubleshooting

### Problem: `google-services.json not found`

Check:

- the file name is exactly `google-services.json`
- the file is in `build-secrets/google-services.json` or another supported build-secrets path
- you are running `build.ps1` from the repository root

### Problem: `Package name mismatch`

Check:

- `ApplicationId` in `EpubReader/EpubReader.csproj`
- `package_name` inside `google-services.json`
- the Android app registration in Firebase

All three must match.

### Problem: Google Sign-In fails with error code `10`

This usually means OAuth / SHA-1 configuration is wrong.

Check:

- Google provider is enabled in Firebase Authentication
- your debug SHA-1 fingerprint is added to the Firebase Android app
- you downloaded a fresh `google-services.json` after updating fingerprints
- the app package name in Firebase matches the app package name in your MAUI project

If you changed the signing key or package name, re-download `google-services.json`.

### Problem: `default_web_client_id` seems wrong or missing

This repo reads the web OAuth client from `google-services.json`.

Check:

```powershell
$json = Get-Content "build-secrets\google-services.json" -Raw | ConvertFrom-Json
$json.client[0].oauth_client | Format-Table client_type, client_id
```

You should see a client with `client_type` equal to `3`.

### Problem: Firebase app does not initialize

Check:

- the JSON file is valid
- the file belongs to your Firebase project
- the build used the correct file
- `FIREBASE_API_KEY`, `FIREBASE_AUTH_DOMAIN`, and `FIREBASE_DATABASE_URL` are present if you are building from explicit parameters or environment variables

### Problem: database access fails

Check:

- Realtime Database was created in your Firebase project
- the URL in `google-services.json` is correct
- your database rules allow the scenario you are testing
- the user is authenticated if your rules require authentication

## Recommended first-run checklist

Use this checklist if you want the shortest path to success:

1. fork the repo
2. keep `ApplicationId` as `com.companyname.epubreader`
3. create a Firebase project
4. register an Android app with package name `com.companyname.epubreader`
5. add the debug SHA-1 fingerprint
   - in PowerShell, run `keytool -list -v -keystore "$env:USERPROFILE\.android\debug.keystore" -alias androiddebugkey -storepass android -keypass android`
   - copy the value shown next to `SHA1:`
   - in Firebase Console, open your Android app settings and add that fingerprint if you did not enter it during app registration
6. enable Google sign-in in Firebase Authentication
7. create Realtime Database
8. download `google-services.json`
9. copy it to `build-secrets/google-services.json`
10. run `./build.ps1 -Android -DebugBuild`
11. launch the app and test Google sign-in

## After you are working locally

Once your local fork works, consider these next steps:

- change `ApplicationTitle` and `ApplicationId` for your fork
- register a new Firebase Android app for the new package name
- download a new `google-services.json`
- create separate Firebase projects for development and production
- review and tighten Realtime Database security rules
- verify you are not committing secrets before pushing changes
