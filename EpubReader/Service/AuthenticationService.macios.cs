using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuthenticationServices;
using Foundation;
using UIKit;

namespace EpubReader.Service;

public partial class AuthenticationService
{
	static readonly HttpClient httpClient = new();

	public async Task<string> SignInWithGooglePlatformAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var (clientId, reversedClientId) = GetGoogleSignInPlistConfig();
		if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(reversedClientId))
		{
			Trace.TraceError("Google sign-in: GoogleService-Info.plist is missing CLIENT_ID or REVERSED_CLIENT_ID");
			throw new InvalidOperationException("Google sign-in requires GoogleService-Info.plist with CLIENT_ID and REVERSED_CLIENT_ID.");
		}

		var nonce = Guid.NewGuid().ToString("N");
		var redirectUri = $"{reversedClientId}:/oauthredirect";
		var authUrl = "https://accounts.google.com/o/oauth2/v2/auth"
			+ $"?client_id={Uri.EscapeDataString(clientId)}"
			+ $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
			+ "&response_type=id_token"
			+ "&scope=" + Uri.EscapeDataString("openid email profile")
			+ $"&nonce={Uri.EscapeDataString(nonce)}"
			+ "&prompt=select_account";

		Trace.TraceInformation("Google sign-in: starting ASWebAuthenticationSession flow");

		var idToken = await GetIdTokenFromWebAuthSessionAsync(authUrl, reversedClientId, cancellationToken);

		if (string.IsNullOrEmpty(idToken))
		{
			Trace.TraceWarning("Google sign-in: completed without ID token");
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
		catch (JsonException)
		{
		}

		return string.IsNullOrWhiteSpace(responseText) ? "Unknown Firebase error." : responseText;
	}

	static JsonSerializerOptions SerializerOptions { get; } = new()
	{
		PropertyNameCaseInsensitive = true
	};

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

	static async Task<string> GetIdTokenFromWebAuthSessionAsync(string authUrl, string callbackScheme, CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

		var session = new ASWebAuthenticationSession(
			new NSUrl(authUrl),
			callbackScheme,
			(callbackUrl, error) =>
			{
				if (error is not null)
				{
					if (error.Code == (int)ASWebAuthenticationSessionErrorCode.CanceledLogin)
					{
						Trace.TraceInformation("Google sign-in: user cancelled ASWebAuthenticationSession");
						tcs.TrySetResult(null);
					}
					else
					{
						Trace.TraceError($"Google sign-in: ASWebAuthenticationSession error - {error.LocalizedDescription}");
						tcs.TrySetException(new NSErrorException(error));
					}
					return;
				}

				var idToken = ExtractIdTokenFromCallbackUrl(callbackUrl);
				Trace.TraceInformation(string.IsNullOrEmpty(idToken)
					? "Google sign-in: callback URL did not contain id_token"
					: "Google sign-in: id_token extracted from callback");
				tcs.TrySetResult(idToken);
			});

		var contextProvider = new AuthPresentationContextProvider();
		session.PresentationContextProvider = contextProvider;
		session.PrefersEphemeralWebBrowserSession = true;

		using var cancellationRegistration = cancellationToken.Register(() =>
		{
			Trace.TraceInformation("Google sign-in: cancellation requested, cancelling ASWebAuthenticationSession");
			session.Cancel();
		});

		try
		{
			session.Start();
			var result = await tcs.Task;
			return result ?? string.Empty;
		}
		catch (OperationCanceledException)
		{
			Trace.TraceWarning("Google sign-in: operation cancelled");
			throw;
		}
		finally
		{
			session.Dispose();
		}
	}

	static string? ExtractIdTokenFromCallbackUrl(NSUrl? callbackUrl)
	{
		if (callbackUrl?.Fragment is null)
		{
			return null;
		}

		var fragment = callbackUrl.Fragment.TrimStart('#');
		var pairs = fragment.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		foreach (var pair in pairs)
		{
			var parts = pair.Split('=', 2);
			if (parts.Length == 2 && string.Equals(parts[0], "id_token", StringComparison.Ordinal))
			{
				return Uri.UnescapeDataString(parts[1]);
			}
		}

		return null;
	}

	sealed class AuthPresentationContextProvider : NSObject, IASWebAuthenticationPresentationContextProviding
	{
		public UIWindow GetPresentationAnchor(ASWebAuthenticationSession session)
		{
			var windowScene = UIApplication.SharedApplication.ConnectedScenes
				.OfType<UIWindowScene>()
				.FirstOrDefault();

			return windowScene?.KeyWindow
				?? throw new InvalidOperationException("No active UIWindowScene found for presenting authentication UI.");
		}
	}
}