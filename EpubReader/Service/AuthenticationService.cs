using System.Diagnostics;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Auth.Repository;
#if ANDROID
using Plugin.Firebase.Auth;
using Android.Gms.Auth.Api.SignIn;
using Android.Gms.Common.Apis;
using Android.App;
using Android.Content;
using Android.Runtime;
#endif

namespace EpubReader.Service;

/// <summary>
/// Firebase authentication service supporting Google OAuth sign-in and local-only mode.
/// </summary>
public sealed class AuthenticationService : IDisposable
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

#if ANDROID
			// Use native Android Google Sign-In to obtain ID token, then exchange with FirebaseAuthClient
			var activity = Platform.CurrentActivity as Android.App.Activity;
			if (activity is null)
			{
				Trace.TraceError("Google sign-in: current Activity is null");
				throw new Exception("Current Activity is null");
			}

			var tcs = new TaskCompletionSource<GoogleSignInAccount?>(TaskCreationOptions.RunContinuationsAsynchronously);

			void OnActivityResult(int requestCode, Result resultCode, Intent? data)
			{
				if (requestCode == 9001)
				{
					Trace.TraceInformation($"Google sign-in: received ActivityResult with resultCode={resultCode}");
					try
					{
						var task = GoogleSignIn.GetSignedInAccountFromIntent(data);
						var account = task?.Result as GoogleSignInAccount;
						Trace.TraceInformation(account is null
							? "Google sign-in: account result is null"
							: "Google sign-in: account result received");
						tcs.TrySetResult(account);
					}
					catch (Exception ex)
					{
						Trace.TraceError($"Google sign-in: GetSignedInAccountFromIntent failed - {ex.Message}");
						tcs.TrySetException(ex);
					}
					finally
					{
						MainActivity.ActivityResult -= OnActivityResult;
					}
				}
			}

			MainActivity.ActivityResult += OnActivityResult;

			// Configure Google Sign-In
			var clientIdResId = activity.Resources?.GetIdentifier("default_web_client_id", "string", activity.PackageName) ?? 0;
			var webClientId = clientIdResId != 0 ? activity.GetString(clientIdResId) : "";

			Trace.TraceInformation($"Google sign-in: resolving default_web_client_id resource (id={clientIdResId})");

			var gsoBuilder = new GoogleSignInOptions.Builder(GoogleSignInOptions.DefaultSignIn).RequestEmail();
			if (!string.IsNullOrEmpty(webClientId))
			{
				Trace.TraceInformation("Google sign-in: requesting IdToken with resolved web client id");
				gsoBuilder = gsoBuilder.RequestIdToken(webClientId);
			}
			var gso = gsoBuilder.Build();

			var googleSignInClient = GoogleSignIn.GetClient(activity, gso);
			var signInIntent = googleSignInClient.SignInIntent;
			Trace.TraceInformation("Google sign-in: launching GoogleSignIn intent (requestCode=9001)");
			activity.StartActivityForResult(signInIntent, 9001);

			var googleAccount = await tcs.Task.ConfigureAwait(false);
			Trace.TraceInformation(googleAccount is null
				? "Google sign-in: Google account task returned null"
				: "Google sign-in: Google account task returned data");

			if (googleAccount != null && !string.IsNullOrEmpty(googleAccount.IdToken))
			{
				Trace.TraceInformation("Google sign-in: IdToken retrieved, exchanging with Firebase");
				var idToken = googleAccount.IdToken;
				// Exchange ID token with Firebase via FirebaseAuthClient
				var authCredential = GoogleProvider.GetCredential(idToken, OAuthCredentialTokenType.IdToken);
				var credential = await firebaseAuthClient.SignInWithCredentialAsync(authCredential).ConfigureAwait(false);

				if (credential?.User is not null)
				{
					Trace.TraceInformation("Google sign-in: Firebase credential obtained, storing secure data");
					await SecureStorage.SetAsync(userIdKey, credential.User.Uid).ConfigureAwait(false);
					await SecureStorage.SetAsync(userEmailKey, credential.User.Info.Email ?? string.Empty).ConfigureAwait(false);
					Preferences.Set(authModeKey, authModeCloud);
					AuthStateChanged?.Invoke(this, true);
					return credential.User.Uid;
				}
			}

			Trace.TraceWarning("Google sign-in: completed without token/credential");

			return string.Empty;
#elif WINDOWS
			Trace.TraceInformation("Google sign-in: starting Windows manual OAuth flow");
			// For Windows, use WebView2-based OAuth flow
			var credential = await SignInWithGoogleManualFlowAsync(cancellationToken);
			if (credential?.User is null)
			{
				Trace.TraceWarning("Google sign-in returned null credential");
				return string.Empty;
			}

			await SecureStorage.SetAsync(userIdKey, credential.User.Uid);
			await SecureStorage.SetAsync(userEmailKey, credential.User.Info.Email ?? string.Empty);
			Preferences.Set(authModeKey, authModeCloud);

			Trace.TraceInformation($"Google sign-in successful: {credential.User.Info.Email}");
			return credential.User.Uid;
#else
			Trace.TraceInformation("Google sign-in: starting iOS/MacCatalyst redirect flow");
			// Use FirebaseProviderType.Google for the redirect on iOS/MacCatalyst
			var credential = await firebaseAuthClient.SignInWithRedirectAsync(
				FirebaseProviderType.Google,
				async uri =>
				{
					// Open browser and wait for redirect
					var redirectUri = await OpenBrowserForOAuthAsync(new Uri(uri));
					return redirectUri.ToString();
				});

			if (credential?.User is null)
			{
				Trace.TraceWarning("Google sign-in returned null credential");
				return string.Empty;
			}

			// Store user information securely
			await SecureStorage.SetAsync(userIdKey, credential.User.Uid);
			await SecureStorage.SetAsync(userEmailKey, credential.User.Info.Email ?? string.Empty);
			Preferences.Set(authModeKey, authModeCloud);

			Trace.TraceInformation($"Google sign-in successful: {credential.User.Info.Email}");
			return credential.User.Uid;
#endif
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

#if WINDOWS || ANDROID
	async Task<UserCredential?> SignInWithGoogleManualFlowAsync(CancellationToken cancellationToken)
	{
		// Construct Google OAuth URL manually
		var authDomain = FirebaseConfig.AuthDomain;
		var apiKey = FirebaseConfig.ApiKey;
		Trace.TraceInformation("Google manual flow: starting OAuth URL construction");
		
		#if ANDROID
		// Use Android Client ID and custom scheme for Android
		var googleClientId = "507277680982-hejogn5jq19j8kvmmr8teei2odg3rlda.apps.googleusercontent.com";
		var redirectUri = "com.googleusercontent.apps.507277680982-hejogn5jq19j8kvmmr8teei2odg3rlda:/oauth2redirect";
		var callbackUrl = new Uri(redirectUri);
#else
		var googleClientId = "507277680982-ivsanmk66uqk5t6dm3f2bbotknjjleg3.apps.googleusercontent.com";
		// Use Firebase's standard auth handler redirect URI for Windows
		var redirectUri = $"https://{authDomain}/__/auth/handler";
		var callbackUrl = new Uri(redirectUri);
		var callbackUrlScheme = $"https://{authDomain}";
#endif
		var scope = "email profile openid";
		Trace.TraceInformation($"Google manual flow: clientId set, redirectUri={redirectUri}, scope={scope}");
		
		// Force account selection on every sign-in to allow switching accounts
		// This prevents automatic sign-in with cached credentials
		var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
			$"client_id={Uri.EscapeDataString(googleClientId)}&" +
			$"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
			$"response_type=id_token&" +
			$"scope={Uri.EscapeDataString(scope)}&" +
			$"prompt=select_account&" +
			$"nonce={Guid.NewGuid():N}";

		Trace.TraceInformation("Google manual flow: opening OAuth URL");

		// Open WebView/WebAuthenticator and get the callback
#if WINDOWS
		var callbackUri = await EpubReader.Platforms.Windows.OAuthWebViewHandler.AuthenticateAsync(
			new Uri(authUrl),
			callbackUrlScheme,
			cancellationToken);
#else
		var result = await WebAuthenticator.Default.AuthenticateAsync(
			new WebAuthenticatorOptions
			{
				Url = new Uri(authUrl),
				CallbackUrl = callbackUrl,
				PrefersEphemeralWebBrowserSession = true
			}, cancellationToken);
		Trace.TraceInformation("Google manual flow: WebAuthenticator completed");
		var callbackUri = new Uri(result.Properties["url"]);
#endif

		Trace.TraceInformation($"Google manual flow: received callback Uri with fragment length={callbackUri.Fragment?.Length ?? 0}");

		// Extract ID token from the fragment
		var fragment = callbackUri.Fragment;
		if (string.IsNullOrEmpty(fragment))
		{
			throw new Exception("No fragment found in callback URL");
		}

		// Parse the fragment (format: #id_token=xxx&...)
		var fragmentParams = System.Web.HttpUtility.ParseQueryString(fragment.TrimStart('#'));
		var idToken = fragmentParams["id_token"];

		if (string.IsNullOrEmpty(idToken))
		{
			throw new Exception("No ID token found in callback");
		}

		Trace.TraceInformation("Google manual flow: ID token received, signing in to Firebase");

		// Sign in to Firebase with the Google ID token using OAuthCredential
		var authCredential = GoogleProvider.GetCredential(idToken, OAuthCredentialTokenType.IdToken);
		var credential = await firebaseAuthClient!.SignInWithCredentialAsync(authCredential);
		Trace.TraceInformation(credential?.User is null
			? "Google manual flow: Firebase credential missing user"
			: "Google manual flow: Firebase credential obtained");
		return credential;
	}
#endif

	static async Task<Uri> OpenBrowserForOAuthAsync(Uri authUri)
	{
		try
		{
#if WINDOWS
			Trace.TraceInformation("OAuth browser flow: launching Windows WebView2 handler");
			// Use WebView2-based OAuth flow for Windows
			return await EpubReader.Platforms.Windows.OAuthWebViewHandler.AuthenticateAsync(
				authUri,
				"http://localhost"); // Firebase Auth redirect URL
#elif IOS || MACCATALYST
			Trace.TraceInformation("OAuth browser flow: launching iOS/MacCatalyst WebAuthenticator");
			// Use WebAuthenticator for iOS/MacCatalyst with redirect flow
			var result = await WebAuthenticator.Default.AuthenticateAsync(
				new WebAuthenticatorOptions
				{
					Url = authUri,
					CallbackUrl = new Uri("http://localhost"),
					PrefersEphemeralWebBrowserSession = true
				});

			// Reconstruct the full callback URL with query parameters
			var uriBuilder = new UriBuilder(result.Properties["url"]);
			return new Uri(uriBuilder.ToString());
#else
			// Fallback for other platforms
			await Browser.Default.OpenAsync(authUri, BrowserLaunchMode.SystemPreferred);
			throw new NotSupportedException(
				"OAuth redirect handling is not implemented for this platform. " +
				"Please use local-only mode or implement platform-specific OAuth handling.");
#endif
		}
		catch (Exception ex)
		{
			Trace.TraceError($"OAuth browser flow failed: {ex.Message}");
			throw;
		}
	}

	public static async Task SetLocalOnlyModeAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			// Generate a local-only user ID
			var localUserId = await SecureStorage.GetAsync(userIdKey);
			if (string.IsNullOrWhiteSpace(localUserId))
			{
				localUserId = $"local-{Guid.NewGuid():N}";
				await SecureStorage.SetAsync(userIdKey, localUserId);
			}

			Preferences.Set(authModeKey, authModeLocal);
			Trace.TraceInformation($"Local-only mode enabled: {localUserId}");
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Failed to set local-only mode: {ex.Message}");
			throw;
		}
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

	public static Task<bool> IsLocalOnlyModeAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var authMode = Preferences.Get(authModeKey, string.Empty);
		return Task.FromResult(authMode == authModeLocal);
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
			// Clear WebView2 cache and cookies to force fresh Google login
			await EpubReader.Platforms.Windows.OAuthWebViewHandler.ClearWebViewDataAsync();
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

	public async Task<bool> NeedsAuthenticationAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Check if user has made a choice (either signed in or chose local mode)
		var authMode = Preferences.Get(authModeKey, string.Empty);
		var userId = await SecureStorage.GetAsync(userIdKey);
		var hasUserId = !string.IsNullOrWhiteSpace(userId);

		return string.IsNullOrWhiteSpace(authMode) && !hasUserId;
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

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;

		if (firebaseAuthClient is not null)
		{
			firebaseAuthClient.AuthStateChanged -= OnAuthStateChanged;
		}

		GC.SuppressFinalize(this);
	}
}
