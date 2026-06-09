using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuthenticationServices;
using Foundation;
using UIKit;
using WebKit;

namespace EpubReader.Service;

public partial class AuthenticationService
{

	public async Task<string> SignInWithGooglePlatformAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var (clientId, reversedClientId) = GetGoogleSignInPlistConfig();
		if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(reversedClientId))
		{
			Trace.TraceError("Google sign-in: GoogleService-Info.plist is missing CLIENT_ID or REVERSED_CLIENT_ID");
			throw new InvalidOperationException("Google sign-in requires GoogleService-Info.plist with CLIENT_ID and REVERSED_CLIENT_ID.");
		}

		// Generate PKCE code verifier and challenge (RFC 7636)
		var codeVerifier = GenerateCodeVerifier();
		var codeChallenge = GenerateCodeChallenge(codeVerifier);

		var redirectUri = $"{reversedClientId}:/oauthredirect";
		var authUrl = "https://accounts.google.com/o/oauth2/v2/auth"
			+ $"?client_id={Uri.EscapeDataString(clientId)}"
			+ $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
			+ "&response_type=code"
			+ "&scope=" + Uri.EscapeDataString("openid email profile")
			+ $"&code_challenge={Uri.EscapeDataString(codeChallenge)}"
			+ "&code_challenge_method=S256"
			+ "&prompt=select_account";

		Trace.TraceInformation("Google sign-in: starting ASWebAuthenticationSession flow (PKCE)");

		var authorizationCode = await GetAuthorizationCodeFromWebAuthSessionAsync(authUrl, reversedClientId, cancellationToken);

		if (string.IsNullOrEmpty(authorizationCode))
		{
			Trace.TraceWarning("Google sign-in: completed without authorization code");
			return string.Empty;
		}

		Trace.TraceInformation("Google sign-in: authorization code retrieved, exchanging for ID token");

		var idToken = await ExchangeCodeForIdTokenAsync(clientId, redirectUri, codeVerifier, authorizationCode, cancellationToken);

		if (string.IsNullOrEmpty(idToken))
		{
			Trace.TraceWarning("Google sign-in: token exchange did not return an ID token");
			return string.Empty;
		}

		Trace.TraceInformation("Google sign-in: ID token retrieved, exchanging with Firebase");

#if IOS
		return await SignInWithFirebaseNativeAsync(idToken, cancellationToken);
#else
		return await SignInWithFirebaseRestAsync(idToken, cancellationToken);
#endif
	}

#if IOS
	async Task<string> SignInWithFirebaseNativeAsync(string idToken, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var auth = Firebase.Auth.Auth.DefaultInstance;
		if (auth is null)
		{
			Trace.TraceError("Google sign-in: Firebase Auth.DefaultInstance is null");
			throw new InvalidOperationException("Firebase Auth is not initialized. Ensure GoogleService-Info.plist is configured.");
		}

		var authCredential = Firebase.Auth.GoogleAuthProvider.GetCredential(idToken, string.Empty);
		if (authCredential is null)
		{
			Trace.TraceError("Google sign-in: failed to create Firebase auth credential from ID token");
			throw new InvalidOperationException("Failed to create Firebase auth credential from ID token.");
		}

		var signInResult = await auth.SignInWithCredentialAsync(authCredential);
		if (signInResult is null)
		{
			Trace.TraceError("Google sign-in: Firebase sign-in with credential returned null");
			throw new InvalidOperationException("Firebase sign-in with credential returned null.");
		}

		if (signInResult.User is null)
		{
			Trace.TraceError("Google sign-in: Firebase sign-in result has null user");
			throw new InvalidOperationException("Firebase sign-in result has null user.");
		}

		Trace.TraceInformation("Google sign-in: Firebase credential obtained, storing secure data");
		await SecureStorage.SetAsync(userIdKey, signInResult.User.Uid);
		await SecureStorage.SetAsync(userEmailKey, signInResult.User.Email ?? string.Empty);
		Preferences.Set(authModeKey, authModeCloud);
		AuthStateChanged?.Invoke(this, true);
		return signInResult.User.Uid;
	}
#else
	async Task<string> SignInWithFirebaseRestAsync(string idToken, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var apiKey = FirebaseConfig.ApiKey;
		if (string.IsNullOrWhiteSpace(apiKey))
		{
			Trace.TraceError("Google sign-in: Firebase API key is not configured");
			throw new InvalidOperationException("Firebase API key is not configured.");
		}

		var firebaseSession = await ExchangeGoogleIdTokenWithFirebaseAsync(idToken, apiKey, cancellationToken);
		await StoreAppleAuthSessionAsync(firebaseSession, cancellationToken);
		AuthStateChanged?.Invoke(this, true);

		Trace.TraceInformation($"Google sign-in successful: {firebaseSession.Email}");
		return firebaseSession.LocalId;
	}

	static async Task<FirebaseGoogleSignInResponse> ExchangeGoogleIdTokenWithFirebaseAsync(string googleIdToken, string apiKey, CancellationToken cancellationToken)
	{
		var request = new FirebaseGoogleSignInRequest
		{
			PostBody = $"id_token={Uri.EscapeDataString(googleIdToken)}&providerId=google.com",
			RequestUri = "http://localhost",
			ReturnIdpCredential = true,
			ReturnSecureToken = true
		};

		var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
		using var response = await httpClient.PostAsync($"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={Uri.EscapeDataString(apiKey)}", content, cancellationToken);
		var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"Firebase sign-in failed: {ExtractFirebaseErrorMessage(responseText)}");
		}

		var firebaseSession = JsonSerializer.Deserialize<FirebaseGoogleSignInResponse>(responseText, SerializerOptions);
		if (firebaseSession is null || string.IsNullOrWhiteSpace(firebaseSession.LocalId) || string.IsNullOrWhiteSpace(firebaseSession.IdToken))
		{
			throw new InvalidOperationException("Firebase sign-in did not return a valid authenticated session.");
		}

		return firebaseSession;
	}

	static async Task StoreAppleAuthSessionAsync(FirebaseGoogleSignInResponse session, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Preferences.Set(authModeKey, authModeCloud);
		await SecureStorage.SetAsync(userIdKey, session.LocalId);
		await SecureStorage.SetAsync(userEmailKey, session.Email ?? string.Empty);
		await SecureStorage.SetAsync(authTokenKey, session.IdToken ?? string.Empty);
		await SecureStorage.SetAsync(refreshTokenKey, session.RefreshToken ?? string.Empty);
		await SecureStorage.SetAsync(authTokenExpirationKey, GetExpirationTimestamp(session.ExpiresIn));
	}

	static string GetExpirationTimestamp(string? expiresInSeconds)
	{
		if (!int.TryParse(expiresInSeconds, out var seconds) || seconds <= 0)
		{
			seconds = 3600;
		}

		return DateTimeOffset.UtcNow.AddSeconds(seconds).ToString("o");
	}

	static string ExtractFirebaseErrorMessage(string responseText)
	{
		try
		{
			var error = JsonSerializer.Deserialize<FirebaseErrorEnvelope>(responseText, SerializerOptions);
			if (!string.IsNullOrWhiteSpace(error?.Error?.Message))
			{
				return error.Error.Message;
			}
		}
		catch (JsonException ex)
		{
			Trace.TraceError($"Failed to parse Firebase error response: {ex.Message}");
		}

		return string.IsNullOrWhiteSpace(responseText) ? "Unknown Firebase error." : responseText;
	}

	sealed class FirebaseGoogleSignInRequest
	{
		[JsonPropertyName("postBody")]
		public string PostBody { get; set; } = string.Empty;
		[JsonPropertyName("requestUri")]
		public string RequestUri { get; set; } = string.Empty;
		[JsonPropertyName("returnIdpCredential")]
		public bool ReturnIdpCredential { get; set; }
		[JsonPropertyName("returnSecureToken")]
		public bool ReturnSecureToken { get; set; }
	}

	sealed class FirebaseGoogleSignInResponse
	{
		[JsonPropertyName("localId")]
		public string LocalId { get; set; } = string.Empty;
		[JsonPropertyName("email")]
		public string? Email { get; set; }
		[JsonPropertyName("idToken")]
		public string? IdToken { get; set; }
		[JsonPropertyName("refreshToken")]
		public string? RefreshToken { get; set; }
		[JsonPropertyName("expiresIn")]
		public string? ExpiresIn { get; set; }
	}

	sealed class FirebaseErrorEnvelope
	{
		[JsonPropertyName("error")]
		public FirebaseErrorBody? Error { get; set; }
	}

	sealed class FirebaseErrorBody
	{
		[JsonPropertyName("message")]
		public string? Message { get; set; }
	}

	sealed class FirebaseRefreshTokenResponse
	{
		[JsonPropertyName("id_token")]
		public string? IdToken { get; set; }
		[JsonPropertyName("refresh_token")]
		public string? RefreshToken { get; set; }
		[JsonPropertyName("expires_in")]
		public string? ExpiresIn { get; set; }
		[JsonPropertyName("user_id")]
		public string? UserId { get; set; }
	}

	internal static async Task<string> GetStoredAuthTokenAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var token = await SecureStorage.GetAsync(authTokenKey);
		var refreshToken = await SecureStorage.GetAsync(refreshTokenKey);
		var expiration = await SecureStorage.GetAsync(authTokenExpirationKey);

		if (string.IsNullOrWhiteSpace(token))
		{
			return string.Empty;
		}

		if (!IsTokenRefreshRequired(expiration))
		{
			return token;
		}

		if (string.IsNullOrWhiteSpace(refreshToken))
		{
			Trace.TraceWarning("Stored Firebase auth token is expired and no refresh token is available.");
			return string.Empty;
		}

		var refreshedSession = await RefreshFirebaseIdTokenAsync(refreshToken, cancellationToken);
		await StoreRefreshedAuthSessionAsync(refreshedSession);
		return refreshedSession.IdToken ?? string.Empty;
	}

	static bool IsTokenRefreshRequired(string? expiration)
	{
		if (!DateTimeOffset.TryParse(expiration, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresAt))
		{
			return true;
		}

		return expiresAt <= DateTimeOffset.UtcNow.AddMinutes(5);
	}

	static async Task<FirebaseRefreshTokenResponse> RefreshFirebaseIdTokenAsync(string refreshToken, CancellationToken cancellationToken)
	{
		var apiKey = FirebaseConfig.ApiKey;
		if (string.IsNullOrWhiteSpace(apiKey))
		{
			throw new InvalidOperationException("Firebase ApiKey is required to refresh the auth session.");
		}

		using var content = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["grant_type"] = "refresh_token",
			["refresh_token"] = refreshToken
		});

		using var response = await httpClient.PostAsync($"https://securetoken.googleapis.com/v1/token?key={Uri.EscapeDataString(apiKey)}", content, cancellationToken);
		var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"Firebase token refresh failed: {ExtractFirebaseErrorMessage(responseText)}");
		}

		var refreshedSession = JsonSerializer.Deserialize<FirebaseRefreshTokenResponse>(responseText, SerializerOptions);
		if (refreshedSession is null || string.IsNullOrWhiteSpace(refreshedSession.IdToken))
		{
			throw new InvalidOperationException("Firebase token refresh did not return a valid ID token.");
		}

		return refreshedSession;
	}

	static async Task StoreRefreshedAuthSessionAsync(FirebaseRefreshTokenResponse session)
	{
		await SecureStorage.SetAsync(authTokenKey, session.IdToken ?? string.Empty);
		await SecureStorage.SetAsync(refreshTokenKey, session.RefreshToken ?? string.Empty);
		await SecureStorage.SetAsync(authTokenExpirationKey, GetExpirationTimestamp(session.ExpiresIn));

		if (!string.IsNullOrWhiteSpace(session.UserId))
		{
			await SecureStorage.SetAsync(userIdKey, session.UserId);
		}
	}
#endif

	static readonly HttpClient httpClient = new();
	static JsonSerializerOptions SerializerOptions { get; } = new()
	{
		PropertyNameCaseInsensitive = true
	};

	static (string clientId, string reversedClientId) GetGoogleSignInPlistConfig()
	{
		try
		{
			var plistPath = NSBundle.MainBundle.PathForResource("GoogleService-Info", "plist");
			if (string.IsNullOrEmpty(plistPath))
			{
				Trace.TraceWarning("Google sign-in: GoogleService-Info.plist not found in app bundle");
				return (string.Empty, string.Empty);
			}

			var plist = NSMutableDictionary.FromFile(plistPath);
			var clientId = plist["CLIENT_ID"] as NSString;
			var reversedClientId = plist["REVERSED_CLIENT_ID"] as NSString;

			if (clientId is null || reversedClientId is null)
			{
				Trace.TraceWarning("Google sign-in: CLIENT_ID or REVERSED_CLIENT_ID missing from GoogleService-Info.plist");
				return (string.Empty, string.Empty);
			}

			Trace.TraceInformation($"Google sign-in config: loaded client ID from plist");
			return (clientId, reversedClientId);
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Google sign-in: failed to read GoogleService-Info.plist - {ex.Message}");
			return (string.Empty, string.Empty);
		}
	}

	static string GenerateCodeVerifier()
	{
		var bytes = new byte[32];
		RandomNumberGenerator.Fill(bytes);
		return Base64UrlEncode(bytes);
	}

	static string GenerateCodeChallenge(string codeVerifier)
	{
		var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
		return Base64UrlEncode(hash);
	}

	static string Base64UrlEncode(byte[] data)
	{
		return Convert.ToBase64String(data)
			.Replace('+', '-')
			.Replace('/', '_')
			.TrimEnd('=');
	}

	static async Task<string> GetAuthorizationCodeFromWebAuthSessionAsync(string authUrl, string callbackScheme, CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

		var windowScene = UIApplication.SharedApplication.ConnectedScenes
			.OfType<UIWindowScene>()
			.FirstOrDefault();
		var rootVC = windowScene?.KeyWindow?.RootViewController
			?? throw new InvalidOperationException("No root view controller found for presenting auth UI.");

		// Walk up the presented view controller chain — MAUI Shell may already have
		// a modal (ControlsModalWrapper) presented, and UIKit rejects presenting on
		// a view controller that is already presenting.
		var presenter = rootVC;
		while (presenter.PresentedViewController is not null)
		{
			presenter = presenter.PresentedViewController;
		}

		var webVC = new GoogleAuthWebViewController(authUrl, callbackScheme, authCode =>
		{
			if (authCode is not null)
			{
				Trace.TraceInformation("Google sign-in: authorization code extracted from callback");
				tcs.TrySetResult(authCode);
			}
			else
			{
				Trace.TraceInformation("Google sign-in: user cancelled or closed the auth view");
				tcs.TrySetResult(null);
			}
		});

		// Wrap in a UINavigationController for the Done button.
		var navController = new UINavigationController(webVC);

		// Full-screen on iPad — no toolbar, no form sheet.
		navController.ModalPresentationStyle = UIModalPresentationStyle.FullScreen;

		using var cancellationRegistration = cancellationToken.Register(() =>
		{
			Trace.TraceInformation("Google sign-in: cancellation requested, dismissing auth view");
			MainThread.BeginInvokeOnMainThread(() =>
			{
				webVC.DismissViewController(true, null);
			});
		});

		try
		{
			await presenter.PresentViewControllerAsync(navController, true);
			var result = await tcs.Task;

			// Dismiss is called inside the web VC callback, but ensure cleanup.
			await navController.DismissViewControllerAsync(true);

			return result ?? string.Empty;
		}
		catch (OperationCanceledException)
		{
			Trace.TraceWarning("Google sign-in: operation cancelled");
			await navController.DismissViewControllerAsync(true);
			throw;
		}
	}

	/// <summary>
	/// Custom WKWebView-based auth controller. Injects CSS to hide the Safari
	/// toolbar buttons (Share / Refresh) that overlay web content on iPad,
	/// and intercepts the OAuth redirect to extract the authorization code.
	/// WKWebView fully supports WebAuthn / passkey flows.
	/// </summary>
	sealed class GoogleAuthWebViewController : UIViewController, IWKNavigationDelegate
	{
		readonly string authUrl;
		readonly string callbackScheme;
		readonly Action<string?> onComplete;
		WKWebView? webView;
		bool completed;

		public GoogleAuthWebViewController(string authUrl, string callbackScheme, Action<string?> onComplete)
		{
			this.authUrl = authUrl;
			this.callbackScheme = callbackScheme;
			this.onComplete = onComplete;

			Title = "Sign in with Google";
			NavigationItem.LeftBarButtonItem = new UIBarButtonItem(
				UIBarButtonSystemItem.Cancel,
				(sender, e) => Finish(null));
		}

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();

			// CSS to suppress the Safari toolbar buttons within the web view.
			const string css = @"
				/* Hide Share and Refresh toolbar overlays injected by the system */
				._sf_toolbar, ._sf_toolbar_container,
				[class*='toolbar'], [class*='Toolbar'],
				[data-original-title='Share'], [data-original-title='Refresh'],
				[aria-label='Share'], [aria-label='Refresh'],
				[title='Share'], [title='Refresh'] { display: none !important; }
				/* Ensure body has enough bottom padding so no content is cut off */
				body { padding-bottom: 0 !important; }";

			var userScript = new WKUserScript(
				new NSString(css),
				WKUserScriptInjectionTime.AtDocumentEnd,
				false);

			var config = new WKWebViewConfiguration();
			config.UserContentController.AddUserScript(userScript);

			var bounds = View is not null ? View.Bounds : UIScreen.MainScreen.Bounds;
			webView = new WKWebView(bounds, config)
			{
				AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
				NavigationDelegate = this
			};
			View?.AddSubview(webView);

			webView.LoadRequest(new NSUrlRequest(new NSUrl(authUrl)));
		}

		void Finish(string? result)
		{
			if (completed)
			{
				return;
			}

			completed = true;
			onComplete(result);
		}

		[Export("webView:decidePolicyForNavigationAction:decisionHandler:")]
		public void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
		{
			var url = navigationAction.Request.Url;
			if (url is not null && url.Scheme == callbackScheme)
			{
				// Intercept the OAuth redirect.
				decisionHandler(WKNavigationActionPolicy.Cancel);
				var authCode = ExtractAuthCodeFromCallbackUrl(url);
				Finish(authCode);
				return;
			}

			// Block reload navigation (prevents the refresh button from working).
			if (navigationAction.NavigationType == WKNavigationType.Reload)
			{
				decisionHandler(WKNavigationActionPolicy.Cancel);
				return;
			}

			decisionHandler(WKNavigationActionPolicy.Allow);
		}
	}

	static string? ExtractAuthCodeFromCallbackUrl(NSUrl? callbackUrl)
	{
		if (callbackUrl?.Query is null)
		{
			return null;
		}

		var query = callbackUrl.Query.TrimStart('?');
		var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		foreach (var pair in pairs)
		{
			var parts = pair.Split('=', 2);
			if (parts.Length == 2 && string.Equals(parts[0], "code", StringComparison.Ordinal))
			{
				return Uri.UnescapeDataString(parts[1]);
			}
		}

		return null;
	}

	static async Task<string> ExchangeCodeForIdTokenAsync(string clientId, string redirectUri, string codeVerifier, string authorizationCode, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var tokenRequestParams = new Dictionary<string, string>
		{
			["code"] = authorizationCode,
			["client_id"] = clientId,
			["redirect_uri"] = redirectUri,
			["grant_type"] = "authorization_code",
			["code_verifier"] = codeVerifier
		};

		using var content = new FormUrlEncodedContent(tokenRequestParams);
		using var response = await httpClient.PostAsync("https://oauth2.googleapis.com/token", content, cancellationToken);
		var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			Trace.TraceError($"Google sign-in: token exchange failed - {responseText}");
			throw new InvalidOperationException($"Google token exchange failed: {ExtractGoogleOAuthErrorMessage(responseText)}");
		}

		var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(responseText, SerializerOptions);
		if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.IdToken))
		{
			Trace.TraceError("Google sign-in: token exchange response did not contain id_token");
			throw new InvalidOperationException("Google token exchange did not return an ID token.");
		}

		Trace.TraceInformation("Google sign-in: successfully exchanged authorization code for ID token");
		return tokenResponse.IdToken;
	}

	static string ExtractGoogleOAuthErrorMessage(string responseText)
	{
		try
		{
			var error = JsonSerializer.Deserialize<GoogleOAuthErrorResponse>(responseText, SerializerOptions);
			if (!string.IsNullOrWhiteSpace(error?.Error))
			{
				return !string.IsNullOrWhiteSpace(error.ErrorDescription)
					? $"{error.Error}: {error.ErrorDescription}"
					: error.Error;
			}
		}
		catch (JsonException ex)
		{
			Trace.TraceError($"Failed to parse Google OAuth error response: {ex.Message}");
		}

		return string.IsNullOrWhiteSpace(responseText) ? "Unknown error." : responseText;
	}

	sealed class GoogleTokenResponse
	{
		[JsonPropertyName("id_token")]
		public string? IdToken { get; set; }
		[JsonPropertyName("access_token")]
		public string? AccessToken { get; set; }
		[JsonPropertyName("refresh_token")]
		public string? RefreshToken { get; set; }
		[JsonPropertyName("expires_in")]
		public int ExpiresIn { get; set; }
		[JsonPropertyName("token_type")]
		public string? TokenType { get; set; }
	}

	sealed class GoogleOAuthErrorResponse
	{
		[JsonPropertyName("error")]
		public string? Error { get; set; }
		[JsonPropertyName("error_description")]
		public string? ErrorDescription { get; set; }
	}
	
}