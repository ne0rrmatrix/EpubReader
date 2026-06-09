using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthenticationServices;
using Foundation;
using UIKit;

#pragma warning disable S1075 // URIs should not be hardcoded

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

			if (plist["CLIENT_ID"] is not NSString clientId || plist["REVERSED_CLIENT_ID"] is not NSString reversedClientId)
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

	static Task<string> GetAuthorizationCodeFromWebAuthSessionAsync(string authUrl, string callbackScheme, CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

		// Use ASWebAuthenticationSession which presents the system's secure Safari
		// browser — required by Google's "Use secure browsers" policy. WKWebView is
		// blocked (error 403: disallowed_useragent).
		var session = new ASWebAuthenticationSession(
			new NSUrl(authUrl),
			callbackScheme,
			(callbackUrl, error) =>
			{
				if (error is not null)
				{
					var nsError = (NSError)error;
					if (nsError.Code == (long)ASWebAuthenticationSessionErrorCode.CanceledLogin)
					{
						Trace.TraceInformation("Google sign-in: user cancelled ASWebAuthenticationSession");
						tcs.TrySetResult(null);
					}
					else
					{
						Trace.TraceError($"Google sign-in: ASWebAuthenticationSession error - {nsError.LocalizedDescription}");
						tcs.TrySetResult(null);
					}
				}
				else
				{
					Trace.TraceInformation("Google sign-in: ASWebAuthenticationSession callback received");
					var authCode = ExtractAuthCodeFromCallbackUrl(callbackUrl);
					tcs.TrySetResult(authCode);
				}
			});

		session.PresentationContextProvider = new AuthPresentationContextProvider();

		using var cancellationRegistration = cancellationToken.Register(() =>
		{
			Trace.TraceInformation("Google sign-in: cancellation requested, cancelling ASWebAuthenticationSession");
			session.Cancel();
		});

		if (!session.Start())
		{
			Trace.TraceError("Google sign-in: ASWebAuthenticationSession.Start() returned false");
			tcs.TrySetException(new InvalidOperationException("Failed to start ASWebAuthenticationSession."));
		}

		return AwaitAuthCodeAsync(tcs, cancellationToken);
	}

	static async Task<string> AwaitAuthCodeAsync(TaskCompletionSource<string?> tcs, CancellationToken cancellationToken)
	{
		try
		{
			await using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
			{
				var result = await tcs.Task;
				return result ?? string.Empty;
			}
		}
		catch (OperationCanceledException)
		{
			Trace.TraceWarning("Google sign-in: operation cancelled");
			throw;
		}
	}

	public static string? ExtractAuthCodeFromCallbackUrl(NSUrl? callbackUrl)
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
}

/// <summary>
/// Provides the presentation anchor window for ASWebAuthenticationSession,
/// which is required to display the secure Safari-based sign-in UI on iOS.
/// </summary>
sealed class AuthPresentationContextProvider : NSObject, IASWebAuthenticationPresentationContextProviding
{
	public UIWindow GetPresentationAnchor(ASWebAuthenticationSession session)
	{
		var windowScene = UIApplication.SharedApplication.ConnectedScenes
			.OfType<UIWindowScene>()
			.FirstOrDefault();
		return windowScene?.KeyWindow
			?? throw new InvalidOperationException("No UIWindow found for presenting ASWebAuthenticationSession.");
	}
}
#pragma warning restore S1075 // URIs should not be hardcoded