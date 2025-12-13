## EpubReader — Copilot instructions (concise)

This file tells an AI coding agent how to be immediately productive in the EpubReader repo (a cross-platform .NET MAUI EPUB reader). Keep edits focused, preserve style, and follow project conventions.

- **Big picture**: .NET MAUI app with MVVM. Core responsibilities are split into:
  - UI & platform shims: `Platforms/`, `Views/`, and `Controls/` (platform files use suffixes like `.android.cs`, `.macios.cs`).
  - Business logic & parsing: `Service/EbookService.cs` (VersOne.Epub + SixLabors.ImageSharp integrations).
  - Persistence & sync: `Database/Db.cs`, `Service/FirebaseSyncService.cs`, and secrets in `build-secrets/`.
  - Messaging & state: `ViewModels/` (MVVM Toolkit attributes) and `Messages/` (WeakReferenceMessenger).

- **Key files to inspect for most tasks**:
  - `Service/EbookService.cs` — EPUB parsing, cover extraction
  - `Database/Db.cs` — SQLite initialization and models
  - `MauiProgram.cs` — DI registration and platform wiring
  - `ViewModels/` — look for `[ObservableProperty]` and `[RelayCommand]`
  - `Messages/` — message classes used with `WeakReferenceMessenger`

- **Build / Run**
  - Primary build script: `build.ps1`. Example debug build:
    ```powershell
    pwsh -File ./build.ps1 -ApiKey <key> -AuthDomain <domain> -DatabaseUrl <url> -Configuration Debug
    ```
  - VS Code tasks available: "Build with Firebase Secrets", "Build (Release) with Firebase Secrets", "Build from .env file", and "Run app (Windows)".
  - Secrets live in `build-secrets/google-services.json` or are passed via `build.ps1` flags. Do not commit secrets.

- **Repository conventions (must follow)**
  - File-scoped namespaces (e.g., `namespace EpubReader.Service;`).
  - Fields use camelCase, no underscore prefix.
  - Public async methods accept `CancellationToken token = default` and call `token.ThrowIfCancellationRequested()` early.
  - Use `Trace.WriteLine()` for logging (not `Debug.WriteLine()`).
  - Enums: index 0 should be `Unknown`/`Default` for safe deserialization.

- **MVVM / messaging patterns**
  - ViewModels inherit `BaseViewModel : ObservableObject`.
  - Properties use `[ObservableProperty]` and commands use `[RelayCommand]` (source-generated code used broadly).
  - Cross-VM communication uses `WeakReferenceMessenger.Default.Send(...)` with message classes under `Messages/`.
  - ViewModels often `Dispose()` and unregister from messages.

- **Platform integration notes**
  - Platform-specific behavior is implemented via files with platform suffixes (`*.android.cs`, `*.macios.cs`, `*.windows.cs`).
  - `MauiProgram.cs` performs DI registration and may call `FirebaseConfigLoader.InjectFirebaseSecrets()` under `#if ANDROID`.

- **When modifying code**
  - Make focused, minimal edits. Preserve style and public APIs.
  - Prefer using repository source-generator patterns (`[ObservableProperty]`, `[RelayCommand]`) over hand-rolled implementations.
  - Run `build.ps1` (or the VS Code tasks) after changes that affect the build.

- **Helpful examples to copy from**
  - `ViewModels/BookViewModel` — shows `[ObservableProperty]`, `[RelayCommand]`, and messenger usage.
  - `Service/FirebaseSyncService.cs` — demonstrates offline queueing and reconciliation.

- **What to avoid**
  - Introducing `NotImplementedException` in production code.
  - Committing any Firebase or secret files into source control.

If anything is unclear or you want more examples/patches, tell me which area to expand and I will iterate.
# Overview
Guidelines for AI agents contributing to **EpubReader**, a cross-platform .NET MAUI EPUB reader with cloud sync, Calibre server integration, and multi-platform support (Windows, Android, iOS, macOS).

## Architecture Overview

### Core Layers
- **EbookService** (`Service/EbookService.cs`): Handles EPUB parsing via VersOne.Epub library, cover extraction, font/image embedding, and synthetic page numbering
- **Database** (`Database/Db.cs`): SQLite wrapper for book metadata, settings, and sync state; auto-initializes tables on first use
- **Authentication** (`Service/AuthenticationService.*.cs`): Platform-specific implementations for Google Firebase auth; supports local-only mode without authentication
- **Sync** (`Service/FirebaseSyncService.cs`): Manages reading progress sync across devices; queues offline changes, reconciles on reconnect
- **MVVM**: ViewModels inherit `ObservableObject` (MVVM Toolkit); communicate via `WeakReferenceMessenger` using message classes in `Messages/`
- **Platform-Specific UI**: Platform folders contain `*.android.cs`, `*.macios.cs`, `*.windows.cs` implementations for WebView handlers, auth, and file pickers

### Key Data Models
- `Book`: Title, author, cover image, reading progress, sync ID
- `Settings`: Theme, font, text size, color scheme preferences; synced across devices
- `ReadingProgress`: Chapter/page position, timestamp for multi-device sync
- `SyncQueueItem`: Tracks offline changes awaiting upload when reconnected

## Build & Firebase Secrets

**Build Script**: `build.ps1` (PowerShell)
- Accepts Firebase secrets via CLI parameters (`-ApiKey`, `-AuthDomain`, `-DatabaseUrl`) or `google-services.json`
- Sets environment variables: `FIREBASE_API_KEY`, `FIREBASE_AUTH_DOMAIN`, `FIREBASE_DATABASE_URL`
- Loads via `FirebaseConfig.cs` at runtime; **never** writes secrets to source code
- Validation: `FirebaseConfigLoader.IsConfigValid()` checks if config is present

**Android**: Injects secrets early in `MauiProgram.cs` via `FirebaseConfigLoader.InjectFirebaseSecrets()`
**Windows/iOS/macOS**: Load from environment variables or assets as fallback

## Best Practices

### Code Style
- **Namespaces**: File-scoped (e.g., `namespace EpubReader.Service;`)
- **Fields**: camelCase without `_` prefix (e.g., `readonly string dbPath = ...;`)
- **Accessibility**: Omit `private` keyword (default in C#); use explicit `public`/`internal` only
- **Null Checking**: Use `is null` / `is not null` pattern; avoid `!` null-forgiving operator
- **Type Checking**: Use `is Type var` pattern instead of casting
- **Collections**: Use C# 12+ collection expressions: `[1, 2, 3]`, `[]`
- **Braces**: Always use `{ }` after control flow (`if`, `for`, `foreach`, etc.)

### Async/Task Patterns
- **CancellationToken**: Required for all `Task`/`ValueTask` methods; provide default value `= default` for public methods, no default for internal
- **CancellationToken Verification**: Use `CancellationToken.ThrowIfCancellationRequested()` in XAML-invoked methods (exceptions cannot be caught in XAML)
- **Logging**: Use `Trace.WriteLine()`, not `Debug.WriteLine()` (Release builds strip Debug output)

### Enums
- **Index 0**: Use `Unknown` for return types with unknown values; use `Default` for option types
- **Naming**: Singular form (`SensorSpeed`, not `SensorSpeeds`)
- **Values**: Explicitly assign 0, 1, 2, 3... unless marked `[Flags]` (required for cross-platform serialization)

### Property Units & Naming
- Include units only if platforms differ (e.g., `PressureInHectopascals` because iOS uses kPa while Android uses hPa)
- Standard units: prefer Hectopascals, degrees implied in names like `HeadingMagneticNorth`

### Database & Models
- **Initialization**: `Db.InitializeAsync()` auto-creates tables on first use (Settings, Book)
- **Sync IDs**: Books auto-generate sync IDs for cloud tracking via `EnsureBookSyncIdAsync()`
- **Attributes**: SQLite-NET uses `[PrimaryKey]`, `[NotNull]`, `[Indexed]` for schema

### MVVM & Messaging
- **ViewModels**: Inherit `BaseViewModel : ObservableObject` (MVVM Toolkit)
- **Properties**: Use `[ObservableProperty]` attribute for auto-generated bindable properties with PropertyChanged events
- **Commands**: Implement as async methods decorated with `[RelayCommand]` for automatic `IAsyncRelayCommand` generation
- **Cross-ViewModel Communication**: Use `WeakReferenceMessenger` with message classes (e.g., `BookMessage(book)`)
- **Disposal**: ViewModels should implement `IDisposable` and unsubscribe from messages in `Dispose()`

### Platform-Specific Code
- **File Organization**: Platform-specific implementations use naming convention (e.g., `FolderPicker.android.cs`, `AuthenticationService.macios.cs`)
- **Conditional Compilation**: Use `#if ANDROID`, `#if IOS || MACCATALYST`, `#if WINDOWS` directives in shared files
- **Platform Handlers**: Register in `MauiProgram.cs` under platform-specific sections (WebViewHandler, etc.)

### EPUB Reading
- **VersOne.Epub**: Use `VersOne.Epub` NuGet package; configure via `EpubReaderOptions` in `EbookService`
- **Cover Extraction**: Default 200x400px; handled in `EbookService.GetListingAsync()`
- **Content Delivery**: HTML/CSS/JS injected into WebView via `Container.js`, `ReadiumCSS`, and `EpubText.js`
- **Image Processing**: SixLabors.ImageSharp for manipulation; FFImageLoading for WebView display

### Service Registration
- **DI Container**: Configured in `MauiProgram.cs` using MAUI's service builder
- **Singleton vs. Transient**: Auth/Sync services are singletons; platform-specific services registered per-platform
- **Firebase Integration**: Plugin.Firebase for cross-platform Firebase API

### Avoid
- `NotImplementedException` (indicates incomplete PR, not a feature to be done later)
- Xamarin.Forms-specific APIs (use .NET MAUI equivalents)
- `Debug.WriteLine()` for logging (use `Trace.WriteLine()`)
- Uncommitted Firebase secrets in source code

## Developer Workflows

### Building
**Prerequisites**: .NET 10 SDK, Visual Studio 2026 with .NET MAUI workload, platform-specific SDKs (Android API 34+, Windows SDK, Xcode 16+ for iOS/macOS)

**With Firebase Secrets**:
```powershell
# Debug build
./build.ps1 -ApiKey "key" -AuthDomain "domain" -DatabaseUrl "url" -Configuration Debug

# Release build
./build.ps1 -ApiKey "key" -AuthDomain "domain" -DatabaseUrl "url" -Configuration Release

# From google-services.json
./build.ps1 -GoogleJsonPath "./build-secrets/google-services.json"

# From .env file (via .vscode/build-from-env.ps1)
./build.ps1
```

**Secrets Management**:
- **Never** commit Firebase secrets to source code
- Place secrets in `build-secrets/google-services.json` (not in repo)
- Environment variables: `FIREBASE_API_KEY`, `FIREBASE_AUTH_DOMAIN`, `FIREBASE_DATABASE_URL`
- CI/CD injects via build parameters; local development uses .env or google-services.json

### Testing & Debugging
- Use Visual Studio's built-in debugger or remote debugging for Android/iOS
- Console output via `Trace.WriteLine()` visible in Visual Studio Output window
- Database file location: `Db.DbPath` (platform-specific user directory)
- Firebase config validation: `FirebaseConfigLoader.IsConfigValid()` before publishing

### Common Patterns
**Async Service Method**:
```csharp
public async Task<Book?> GetBookAsync(string path, CancellationToken token = default)
{
    token.ThrowIfCancellationRequested();
    // implementation
}
```

**ViewModel with Messaging**:
```csharp
public partial class BookViewModel : BaseViewModel
{
    [ObservableProperty] Book? currentBook;
    
    [RelayCommand]
    private async Task LoadBookAsync(CancellationToken token)
    {
        var book = await ebookService.GetListingAsync(path);
        CurrentBook = book;
        WeakReferenceMessenger.Default.Send(new BookMessage(book));
    }
    
    public override void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<BookMessage>(this);
        base.Dispose();
    }
}
```

**Platform-Specific Registration in MauiProgram**:
```csharp
#if ANDROID
FirebaseConfigLoader.InjectFirebaseSecrets();
#endif
```
