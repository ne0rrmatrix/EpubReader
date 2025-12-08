# build-secrets (local-only)

This folder is intended to hold local secrets for development and MUST NOT be committed to source control.

Current policy (2025):

- The app no longer relies on Android resource `strings.xml` to hold Firebase credentials. Firebase configuration is loaded at runtime from environment variables or a `google-services.json` file that is *read* (not copied) by the build script.
- Preferred (CI / secure): set the following environment variables in your CI or local shell before building:
  - `FIREBASE_APP_ID` (mobilesdk_app_id)
  - `FIREBASE_API_KEY` (current_key)
  - `FIREBASE_DATABASE_URL` (firebase_url)
  - `FIREBASE_AUTH_DOMAIN` (optional, e.g. projectid.firebaseapp.com)
  - `FIREBASE_WEB_CLIENT_ID` (optional)

- Alternative (local dev): place your `google-services.json` inside one of these local-only locations and the build script will read it to set missing environment variables (it does not copy the file into the project):
  - `build-secrets/google-services.json`
  - `build-secrets/android/google-services.json`
  - `EpubReader/build-secrets/google-services.json`
  - `EpubReader/build-secrets/android/google-services.json`

Important notes:
- The build script `build.ps1` will only *read* `google-services.json` to extract keys and set environment variables; it will NOT copy that file into project resources. This keeps secrets out of the committed artifact.
- Do NOT commit `google-services.json` or any secret file. Add `build-secrets/` to `.gitignore` if it is not already present.

How to build (examples):

- Build Android (Debug):

  ```powershell
  ./build.ps1 -Android -DebugBuild
  ```

- Build Windows (Release):

  ```powershell
  ./build.ps1 -Windows -ReleaseBuild
  ```

- Provide a google json for parsing (local dev):

  ```powershell
  ./build.ps1 -GoogleJsonPath C:\path\to\google-services.json -Android -DebugBuild
  ```

This script extracts the required keys and sets `FIREBASE_*` environment variables before invoking `dotnet build`.

If you run into missing-values errors, set the required env vars explicitly or place your `google-services.json` in one of the `build-secrets` locations above.