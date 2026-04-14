using System.Diagnostics;
using Plugin.Firebase.Auth;

namespace EpubReader.Service;

public partial class AuthenticationService : IDisposable
{
	const string authModeKey = "Auth.Mode";
	const string authModeLocal = "local";
	const string authModeCloud = "cloud";
	const string userIdKey = "Auth.UserId";
	const string userEmailKey = "Auth.UserEmail";
	const string authTokenKey = "Auth.Token";
	const string refreshTokenKey = "Auth.RefreshToken";
	const string authTokenExpirationKey = "Auth.TokenExpirationUtc";

	IFirebaseAuth? firebaseAuth;
	IDisposable? authStateListenerSubscription;
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

		if (disposing)
		{
			authStateListenerSubscription?.Dispose();
			authStateListenerSubscription = null;
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

	public static async Task SetLocalOnlyModeAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Preferences.Set(authModeKey, authModeLocal);
		SecureStorage.Remove(userEmailKey);
		SecureStorage.Remove(authTokenKey);
		SecureStorage.Remove(refreshTokenKey);
		SecureStorage.Remove(authTokenExpirationKey);

		var userId = await SecureStorage.GetAsync(userIdKey);
		if (string.IsNullOrWhiteSpace(userId) || !userId.StartsWith("local-", StringComparison.Ordinal))
		{
			await SecureStorage.SetAsync(userIdKey, $"local-{Guid.NewGuid():N}");
		}

		Trace.TraceInformation("Local-only mode enabled");
	}

	public async Task<string> SignInWithGoogleAsync(CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		cancellationToken.ThrowIfCancellationRequested();

#if !WINDOWS
		if (!CrossFirebaseAuth.IsSupported)
		{
			Trace.TraceWarning("Google sign-in is not supported on this platform by the installed Firebase auth plugin.");
			throw new PlatformNotSupportedException("Google sign-in is not supported on this platform by the installed Firebase auth plugin.");
		}
#else
		if (!CrossFirebaseAuth.IsSupported)
		{
			Trace.TraceInformation("Native Firebase auth plugin is unavailable on Windows; using the WebView-based Google sign-in flow.");
		}
#endif

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
		if (firebaseAuth?.CurrentUser is not null)
		{
			try
			{
				var tokenResult = await firebaseAuth.CurrentUser.GetIdTokenResultAsync(false);
				return tokenResult.Token ?? string.Empty;
			}
			catch (Exception ex)
			{
				Trace.TraceError($"Failed to get Firebase token: {ex.Message}");
			}
		}

#if WINDOWS
		var storedToken = await GetStoredAuthTokenAsync(cancellationToken);
		if (!string.IsNullOrWhiteSpace(storedToken))
		{
			return storedToken;
		}
#endif

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
		if (firebaseAuth?.CurrentUser is not null)
		{
			return true;
		}

#if WINDOWS
		var storedToken = await GetStoredAuthTokenAsync(cancellationToken);
		if (!string.IsNullOrWhiteSpace(storedToken) && authMode == authModeCloud)
		{
			return true;
		}
#endif

		// Check stored credentials
		var userId = await SecureStorage.GetAsync(userIdKey);
		return !string.IsNullOrWhiteSpace(userId) && authMode == authModeCloud;
	}

	public async Task<string> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Try Firebase user first
		if (firebaseAuth?.CurrentUser is not null)
		{
			return firebaseAuth.CurrentUser.Uid;
		}

		// Fall back to stored user ID
		var userId = await SecureStorage.GetAsync(userIdKey);
		return userId ?? string.Empty;
	}

	public async Task<string> GetCurrentUserEmailAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Try Firebase user first
		if (firebaseAuth?.CurrentUser?.Email is not null)
		{
			return firebaseAuth.CurrentUser.Email;
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
			if (firebaseAuth is not null)
			{
				await firebaseAuth.SignOutAsync();
			}

			// Clear stored credentials
			SecureStorage.Remove(userIdKey);
			SecureStorage.Remove(userEmailKey);
			SecureStorage.Remove(authTokenKey);
			SecureStorage.Remove(refreshTokenKey);
			SecureStorage.Remove(authTokenExpirationKey);
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

	void InitializeFirebaseAuthClient()
	{
		try
		{
			if (!CrossFirebaseAuth.IsSupported)
			{
				Trace.TraceWarning("Firebase Authentication is not supported on this platform by the installed Firebase auth plugin.");
				return;
			}

			firebaseAuth = CrossFirebaseAuth.Current;
			authStateListenerSubscription = firebaseAuth.AddAuthStateListener(OnAuthStateChanged);

			Trace.TraceInformation("Firebase Authentication initialized successfully");
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Failed to initialize Firebase Authentication: {ex.Message}");
		}
	}

	void OnAuthStateChanged(IFirebaseAuth auth)
	{
		var isAuthenticated = auth.CurrentUser is not null;
		Trace.TraceInformation($"Authentication state changed. Authenticated: {isAuthenticated}");
		AuthStateChanged?.Invoke(this, isAuthenticated);
	}
}