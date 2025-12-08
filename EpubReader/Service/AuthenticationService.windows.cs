using System.Diagnostics;
using Firebase.Auth;
using Firebase.Auth.Providers;

namespace EpubReader.Service;

public partial class AuthenticationService
{
	public async Task<string> SignInWithGooglePlatformAsync(CancellationToken cancellationToken)
	{
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
	}

	async Task<UserCredential?> SignInWithGoogleManualFlowAsync(CancellationToken cancellationToken)
	{
		// Construct Google OAuth URL manually
		Trace.TraceInformation("Google manual flow: starting OAuth URL construction");
		
		var googleClientId = "507277680982-ivsanmk66uqk5t6dm3f2bbotknjjleg3.apps.googleusercontent.com";
		// Use Firebase's standard auth handler redirect URI for Windows
		var redirectUri = $"https://{FirebaseConfig.AuthDomain}/__/auth/handler";
		var callbackUrlScheme = $"https://{FirebaseConfig.AuthDomain}";
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
		var callbackUri = await EpubReader.Platforms.Windows.OAuthWebViewHandler.AuthenticateAsync(
			new Uri(authUrl),
			callbackUrlScheme,
			cancellationToken);

		Trace.TraceInformation($"Google manual flow: received callback Uri with fragment length={callbackUri.Fragment?.Length ?? 0}");

		// Extract ID token from the fragment
		var fragment = callbackUri.Fragment;
		if (string.IsNullOrEmpty(fragment))
		{
			throw new InvalidOperationException("No fragment found in callback URL");
		}

		// Parse the fragment (format: #id_token=xxx&...)
		var fragmentParams = System.Web.HttpUtility.ParseQueryString(fragment.TrimStart('#'));
		var idToken = fragmentParams["id_token"];

		if (string.IsNullOrEmpty(idToken))
		{
			throw new InvalidOperationException("No ID token found in callback");
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

	public static async Task ClearPlatformAuthDataAsync()
	{
		// Clear WebView2 cache and cookies to force fresh Google login
		await EpubReader.Platforms.Windows.OAuthWebViewHandler.ClearWebViewDataAsync();
	}

	public static async Task SetLocalOnlyModeAsync(CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}
}
