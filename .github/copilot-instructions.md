# Copilot Instructions

## EpubReader — Copilot instructions (concise)

This file tells an AI coding agent how to be immediately productive in the EpubReader repo (a cross-platform .NET MAUI EPUB reader). Keep edits focused, preserve style, and follow project conventions.

- **Big picture**: .NET MAUI app with MVVM. Core responsibilities are split into:
  - UI & platform shims: `Platforms/`, `Views/`, and `Controls/` (platform files use suffixes like `.android.cs`, `.macios.cs`).

- **Key files to inspect for most tasks**:
  - `Service/EbookService.cs` — EPUB parsing, cover extraction
  - `Database/Db.cs` — SQLite initialization and models
  - `MauiProgram.cs` — DI registration and platform wiring
  - `ViewModels/` — look for `[ObservableProperty]` and `[RelayCommand]`
 
- **Repository conventions (must follow)**
  - File-scoped namespaces (e.g., `namespace EpubReader.Service;`).
  - Fields use camelCase, no underscore prefix.
  - Public async methods accept `CancellationToken token = default` and call `token.ThrowIfCancellationRequested()` early.
  - Use `Trace.WriteLine()` for logging (not `Debug.WriteLine()`).
  - Enums: index 0 should be `Unknown`/`Default` for safe deserialization.

- **MVVM / messaging patterns**
  - ViewModels inherit `BaseViewModel : ObservableObject`.
  - Properties use `[ObservableProperty]` and commands use `[RelayCommand]` (source-generated code used broadly).
  - Prefer `[ObservableProperty]` for bindable view model properties in this repo.
  - Cross-VM communication uses `WeakReferenceMessenger.Default.Send(...)` with message classes under `Messages/`.
  - ViewModels often `Dispose()` and unregister from messages.
  - Use view models and MVVM patterns for UI components in this repo.

- **Platform integration notes**
  - Platform-specific behavior is implemented via files with platform suffixes (`*.android.cs`, `*.macios.cs`, `*.windows.cs`).
  - `MauiProgram.cs` performs DI registration and may call `FirebaseConfigLoader.InjectFirebaseSecrets()` under `#if ANDROID`.

- **When modifying code**
  - Make focused, minimal edits. Preserve style and public APIs.
  - Prefer using repository source-generator patterns (`[ObservableProperty]`, `[RelayCommand]`) over hand-rolled implementations.

- **Helpful examples to copy from**
  - `ViewModels/BookViewModel` — shows `[ObservableProperty]`, `[RelayCommand]`, and messenger usage.
  - `Service/FirebaseSyncService.cs` — demonstrates offline queueing and reconciliation.

- **What to avoid**
  - Introducing `NotImplementedException` in production code.
  - Committing any Firebase or secret files into source control.

If anything is unclear or you want more examples/patches, tell me which area to expand and I will iterate.

## Overview
Guidelines for AI agents contributing to **EpubReader**, a cross-platform .NET MAUI EPUB reader with cloud sync, Calibre server integration, and multi-platform support (Windows, Android, iOS, macOS).

## Architecture Overview

### Core Layers
- **MVVM**: ViewModels inherit `ObservableObject` (MVVM Toolkit); communicate via `WeakReferenceMessenger` using message classes in `Messages/`.
- **Platform-Specific UI**: Platform folders contain `*.android.cs`, `*.macios.cs`, `*.windows.cs` implementations for WebView handlers, auth, and file pickers.

### Key Data Models
- `Book`: Title, author, cover image, reading progress, sync ID.
- `Settings`: Theme, font, text size, color scheme preferences; synced across devices.
- `ReadingProgress`: Chapter/page position, timestamp for multi-device sync.
- `SyncQueueItem`: Tracks offline changes awaiting upload when reconnected.

## Best Practices

### Code Style
- **Namespaces**: File-scoped (e.g., `namespace EpubReader.Service;`).
- **Fields**: camelCase without `_` prefix (e.g., `readonly string dbPath = ...;`).
- **Accessibility**: Omit `private` keyword (default in C#); use explicit `public`/`internal` only.
- **Null Checking**: Use `is null` / `is not null` pattern; avoid `!` null-forgiving operator.
- **Type Checking**: Use `is Type var` pattern instead of casting.
- **Collections**: Use C# 12+ collection expressions: `[1, 2, 3]`, `[]`.
- **Braces**: Always use `{ }` after control flow (`if`, `for`, `foreach`, etc.).

### Async/Task Patterns
- **CancellationToken**: Required for all `Task`/`ValueTask` methods; provide default value `= default` for public methods, no default for internal.
- **CancellationToken Verification**: Use `CancellationToken.ThrowIfCancellationRequested()` in XAML-invoked methods (exceptions cannot be caught in XAML).
- **Logging**: Use `Trace.WriteLine()`, not `Debug.WriteLine()` (Release builds strip Debug output).

### Enums
- **Index 0**: Use `Unknown` for return types with unknown values; use `Default` for option types.
- **Naming**: Singular form (`SensorSpeed`, not `SensorSpeeds`).
- **Values**: Explicitly assign 0, 1, 2, 3... unless marked `[Flags]` (required for cross-platform serialization).

### Property Units & Naming
- Include units only if platforms differ (e.g., `PressureInHectopascals` because iOS uses kPa while Android uses hPa).
- Standard units: prefer Hectopascals, degrees implied in names like `HeadingMagneticNorth`.

### MVVM & Messaging
- **ViewModels**: Inherit `BaseViewModel : ObservableObject` (MVVM Toolkit).
- **Properties**: Use `[ObservableProperty]` attribute for auto-generated bindable properties with PropertyChanged events.
- **Commands**: Implement as async methods decorated with `[RelayCommand]` for automatic `IAsyncRelayCommand` generation.
- **Cross-ViewModel Communication**: Use `WeakReferenceMessenger` with message classes (e.g., `BookMessage(book)`).
- **Disposal**: ViewModels should implement `IDisposable` and unsubscribe from messages in `Dispose()`.

### Platform-Specific Code
- **File Organization**: Platform-specific implementations use naming convention (e.g., `FolderPicker.android.cs`, `AuthenticationService.macios.cs`).
- **Conditional Compilation**: Use `#if ANDROID`, `#if IOS || MACCATALYST`, `#if WINDOWS` directives in shared files.
- **Platform Handlers**: Register in `MauiProgram.cs` under platform-specific sections (WebViewHandler, etc.).



### Common Patterns
**Async Service Method**:public async Task<Book?> GetBookAsync(string path, CancellationToken token = default)
{
    token.ThrowIfCancellationRequested();
    // implementation
}**ViewModel with Messaging**:public partial class BookViewModel : BaseViewModel
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
}**Platform-Specific Registration in MauiProgram**:#if ANDROID
FirebaseConfigLoader.InjectFirebaseSecrets();
#endif### Floating-Point Comparisons
- When comparing floating-point values, use explicit epsilon ranges and avoid equality-style fallbacks in nullable comparison helpers.

### Reader Architecture
- Keep `index.html` plus the iframe, but use only one readiness event for the initial iframe load path.

### Exception Handling
- Do not add try/catch by default. Use exception handling only when it is required to prevent an app crash or handle a known expected failure; avoid unnecessary try/catch blocks for performance and clarity.
