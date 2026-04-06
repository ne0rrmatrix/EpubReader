using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EpubReader.Service;

public partial class AuthenticationService
{
	static readonly HttpClient httpClient = new();

	public async Task<string> SignInWithGooglePlatformAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var apiKey = FirebaseConfig.ApiKey;
		var authDomain = FirebaseConfig.AuthDomain;
		var googleClientId = FirebaseConfig.DefaultWebClientId;

		if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(authDomain) || string.IsNullOrWhiteSpace(googleClientId))
		{
			Trace.TraceWarning("Windows Google sign-in is missing Firebase configuration values.");
			throw new InvalidOperationException("Windows Google sign-in requires Firebase ApiKey, AuthDomain, and DefaultWebClientId configuration.");
		}

		var redirectUri = $"https://{authDomain}/__/auth/handler";
		var authUrl = BuildGoogleAuthUrl(googleClientId, redirectUri);

		Trace.TraceInformation("Google sign-in: starting Windows OAuth flow");
		var callbackUri = await EpubReader.Platforms.Windows.OAuthWebViewHandler.AuthenticateAsync(new Uri(authUrl), redirectUri, cancellationToken);
		var googleIdToken = ExtractIdTokenFromCallback(callbackUri);
		if (string.IsNullOrWhiteSpace(googleIdToken))
		{
			Trace.TraceWarning("Windows OAuth flow completed without a Google ID token.");
			return string.Empty;
		}

		var firebaseSession = await ExchangeGoogleIdTokenWithFirebaseAsync(googleIdToken, redirectUri, apiKey, cancellationToken);
		await StoreWindowsAuthSessionAsync(firebaseSession, cancellationToken);
		AuthStateChanged?.Invoke(this, true);

		Trace.TraceInformation($"Google sign-in successful: {firebaseSession.Email}");
		return firebaseSession.LocalId;
	}

	static string BuildGoogleAuthUrl(string clientId, string redirectUri)
	{
		var nonce = Guid.NewGuid().ToString("N");
		var state = Guid.NewGuid().ToString("N");
		var query = new StringBuilder("https://accounts.google.com/o/oauth2/v2/auth?");
		query.Append($"client_id={Uri.EscapeDataString(clientId)}");
		query.Append($"&redirect_uri={Uri.EscapeDataString(redirectUri)}");
		query.Append("&response_type=id_token");
		query.Append($"&scope={Uri.EscapeDataString("openid email profile")}");
		query.Append("&prompt=select_account");
		query.Append($"&nonce={Uri.EscapeDataString(nonce)}");
		query.Append($"&state={Uri.EscapeDataString(state)}");
		return query.ToString();
	}

	static string ExtractIdTokenFromCallback(Uri callbackUri)
	{
		var fragment = callbackUri.Fragment;
		if (string.IsNullOrWhiteSpace(fragment))
		{
			return string.Empty;
		}

		var pairs = fragment.TrimStart('#')
			.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		foreach (var pair in pairs)
		{
			var parts = pair.Split('=', 2);
			if (parts.Length != 2)
			{
				continue;
			}

			if (string.Equals(parts[0], "id_token", StringComparison.Ordinal))
			{
				return Uri.UnescapeDataString(parts[1]);
			}
		}

		return string.Empty;
	}

	static async Task<FirebaseGoogleSignInResponse> ExchangeGoogleIdTokenWithFirebaseAsync(string googleIdToken, string redirectUri, string apiKey, CancellationToken cancellationToken)
	{
		var request = new FirebaseGoogleSignInRequest
		{
			PostBody = $"id_token={Uri.EscapeDataString(googleIdToken)}&providerId=google.com",
			RequestUri = redirectUri,
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
			Trace.TraceWarning("Stored Windows Firebase auth token is expired and no refresh token is available.");
			return string.Empty;
		}

		var refreshedSession = await RefreshFirebaseIdTokenAsync(refreshToken, cancellationToken);
		await StoreRefreshedWindowsAuthSessionAsync(refreshedSession);
		return refreshedSession.IdToken ?? string.Empty;
	}

	static bool IsTokenRefreshRequired(string? expiration)
	{
		if (!DateTimeOffset.TryParse(expiration, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt))
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
			throw new InvalidOperationException("Firebase ApiKey is required to refresh a Windows auth session.");
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

	static async Task StoreWindowsAuthSessionAsync(FirebaseGoogleSignInResponse session, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		Preferences.Set(authModeKey, authModeCloud);
		await SecureStorage.SetAsync(userIdKey, session.LocalId);
		await SecureStorage.SetAsync(userEmailKey, session.Email ?? string.Empty);
		await SecureStorage.SetAsync(authTokenKey, session.IdToken ?? string.Empty);
		await SecureStorage.SetAsync(refreshTokenKey, session.RefreshToken ?? string.Empty);
		await SecureStorage.SetAsync(authTokenExpirationKey, GetExpirationTimestamp(session.ExpiresIn));
	}

	static async Task StoreRefreshedWindowsAuthSessionAsync(FirebaseRefreshTokenResponse session)
	{
		await SecureStorage.SetAsync(authTokenKey, session.IdToken ?? string.Empty);
		await SecureStorage.SetAsync(refreshTokenKey, session.RefreshToken ?? string.Empty);
		await SecureStorage.SetAsync(authTokenExpirationKey, GetExpirationTimestamp(session.ExpiresIn));

		if (!string.IsNullOrWhiteSpace(session.UserId))
		{
			await SecureStorage.SetAsync(userIdKey, session.UserId);
		}
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
			// Ignore parsing failures and fall back to the raw response.
		}

		return string.IsNullOrWhiteSpace(responseText) ? "Unknown Firebase error." : responseText;
	}

	static JsonSerializerOptions SerializerOptions { get; } = new()
	{
		PropertyNameCaseInsensitive = true
	};

	public static async Task ClearPlatformAuthDataAsync()
	{
		// Clear WebView2 cache and cookies to force fresh Google login
		await EpubReader.Platforms.Windows.OAuthWebViewHandler.ClearWebViewDataAsync();
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
}