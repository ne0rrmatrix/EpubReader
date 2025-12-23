using System.Diagnostics;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Auth.Repository;

namespace EpubReader.Service;

public partial class AuthenticationService : IDisposable
{
	const string authModeKey = "Auth.Mode";
	const string authModeLocal = "local";
	const string authModeCloud = "cloud";
	const string userIdKey = "Auth.UserId";
	const string userEmailKey = "Auth.UserEmail";

	FirebaseAuthClient? firebaseAuthClient;
	bool disposed;

	public event EventHandler<bool>? AuthStateChanged;

	public AuthenticationService()
	{
		InitializeFirebaseAuthClient();
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposed)
		{
			return;
		}

		disposed = true;

		if (disposing && firebaseAuthClient is not null)
		{
			firebaseAuthClient.AuthStateChanged -= OnAuthStateChanged;
		}
	}

	public static async Task<bool> NeedsAuthenticationAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Check if user has made a choice (either signed in or chose local mode)
		var authMode = Preferences.Get(authModeKey, string.Empty);
		var userId = await SecureStorage.GetAsync(userIdKey);
		var hasUserId = !string.IsNullOrWhiteSpace(userId);

		return string.IsNullOrWhiteSpace(authMode) && !hasUserId;
	}

	public static Task<bool> IsLocalOnlyModeAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var authMode = Preferences.Get(authModeKey, string.Empty);
		return Task.FromResult(authMode == authModeLocal);
	}

	public async Task<string> SignInWithGoogleAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (firebaseAuthClient is null)
		{
			Trace.TraceError("Google sign-in: firebaseAuthClient not initialized");
			throw new InvalidOperationException("Firebase Authentication is not configured. Please configure Firebase.ApiKey and Firebase.AuthDomain.");
		}

		try
		{
			Trace.TraceInformation("Google sign-in: starting platform flow");
			return await SignInWithGooglePlatformAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
			Trace.TraceInformation("Google sign-in cancelled by user");
			throw;
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Google sign-in failed: {ex}");
			throw;
		}
	}


	public async Task<string> GetAuthTokenAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Get Firebase ID token if authenticated
		if (firebaseAuthClient?.User is not null)
		{
			try
			{
				var token = await firebaseAuthClient.User.GetIdTokenAsync();
				return token;
			}
			catch (Exception ex)
			{
				Trace.TraceError($"Failed to get Firebase token: {ex.Message}");
			}
		}

		// For local mode, return empty string
		return string.Empty;
	}

	public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var authMode = Preferences.Get(authModeKey, string.Empty);

		if (authMode == authModeLocal)
		{
			return false; // Local mode is not considered "authenticated" for cloud purposes
		}

		// Check if Firebase user exists
		if (firebaseAuthClient?.User is not null)
		{
			return true;
		}

		// Check stored credentials
		var userId = await SecureStorage.GetAsync(userIdKey);
		return !string.IsNullOrWhiteSpace(userId) && authMode == authModeCloud;
	}

	public async Task<string> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Try Firebase user first
		if (firebaseAuthClient?.User is not null)
		{
			return firebaseAuthClient.User.Uid;
		}

		// Fall back to stored user ID
		var userId = await SecureStorage.GetAsync(userIdKey);
		return userId ?? string.Empty;
	}

	public async Task<string> GetCurrentUserEmailAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Try Firebase user first
		if (firebaseAuthClient?.User?.Info?.Email is not null)
		{
			return firebaseAuthClient.User.Info.Email;
		}

		// Fall back to stored email
		var email = await SecureStorage.GetAsync(userEmailKey);
		return email ?? string.Empty;
	}

	public async Task SignOutAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			// Sign out from Firebase
			firebaseAuthClient?.SignOut();

			// Clear stored credentials
			SecureStorage.Remove(userIdKey);
			SecureStorage.Remove(userEmailKey);

			// Clear Firebase user repository cache to force re-authentication
			// This ensures the user must provide credentials on next sign-in
			await ClearFirebaseUserCacheAsync();
#if WINDOWS
			// Platform-specific cleanup
			await ClearPlatformAuthDataAsync();
#endif
			// Switch to local mode
			Preferences.Set(authModeKey, authModeLocal);

			// Generate new local user ID
			var localUserId = $"local-{Guid.NewGuid():N}";
			await SecureStorage.SetAsync(userIdKey, localUserId);

			Trace.TraceInformation("User signed out, switched to local mode");
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Sign out failed: {ex.Message}");
			throw;
		}
	}

	static async Task ClearFirebaseUserCacheAsync()
	{
		try
		{
			// Clear the FileUserRepository cache
			// The cache is stored in the user's application data folder
			var appDataPath = FileSystem.AppDataDirectory;
			var firebaseUserFile = Path.Combine(appDataPath, "EpubReader.json");

			if (File.Exists(firebaseUserFile))
			{
				await Task.Run(() => File.Delete(firebaseUserFile));
				Trace.TraceInformation("Firebase user cache cleared");
			}
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"Failed to clear Firebase user cache: {ex.Message}");
			// Don't throw - this is a cleanup operation
		}
	}
	void InitializeFirebaseAuthClient()
	{
		try
		{
			var apiKey = FirebaseConfig.ApiKey;
			var authDomain = FirebaseConfig.AuthDomain;

			Trace.TraceInformation("Auth init: loading Firebase configuration");

			if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(authDomain))
			{
				Trace.TraceWarning("Firebase not configured. Set FIREBASE_API_KEY and FIREBASE_AUTH_DOMAIN environment variables or pass MSBuild properties.");
				return;
			}

			var config = new FirebaseAuthConfig
			{
				ApiKey = apiKey,
				AuthDomain = authDomain,
				Providers =
				[
					new GoogleProvider().AddScopes("email"),
                    // Apple provider - uncomment when ready to implement:
                    // new AppleProvider()
                    // Note: Apple Sign-In requires:
                    // 1. Apple Developer Program membership
                    // 2. Configure Sign in with Apple capability in your app
                    // 3. Add Service ID in Apple Developer portal
                    // 4. Configure redirect URLs
                ],
				UserRepository = new FileUserRepository("EpubReader")
			};

			firebaseAuthClient = new FirebaseAuthClient(config);

			// Subscribe to auth state changes
			firebaseAuthClient.AuthStateChanged += OnAuthStateChanged;

			Trace.TraceInformation("Firebase Authentication initialized successfully");
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Failed to initialize Firebase Authentication: {ex.Message}");
		}
	}

	void OnAuthStateChanged(object? sender, UserEventArgs e)
	{
		if (e.User is not null)
		{
			Trace.TraceInformation($"Auth state changed: User signed in - {e.User.Info.Email}");
			Preferences.Set(authModeKey, authModeCloud);
			AuthStateChanged?.Invoke(this, true);
		}
		else
		{
			Trace.TraceInformation("Auth state changed: User signed out");
			Preferences.Set(authModeKey, authModeLocal);
			AuthStateChanged?.Invoke(this, false);
		}
	}
}