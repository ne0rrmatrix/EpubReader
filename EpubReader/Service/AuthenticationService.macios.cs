using System.Diagnostics;
using Firebase.Auth;

namespace EpubReader.Service;

public partial class AuthenticationService
{
	public async Task<string> SignInWithGooglePlatformAsync(CancellationToken cancellationToken)
	{
		Trace.TraceInformation("Google sign-in: starting iOS/MacCatalyst redirect flow");
		// Use FirebaseProviderType.Google for the redirect on iOS/MacCatalyst
		var credential = await firebaseAuthClient!.SignInWithRedirectAsync(
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
	}

	static async Task<Uri> OpenBrowserForOAuthAsync(Uri authUri)
	{
		try
		{
			Trace.TraceInformation("OAuth browser flow: launching iOS/MacCatalyst WebAuthenticator");
			// Use WebAuthenticator for iOS/MacCatalyst with redirect flow
			var result = await WebAuthenticator.Default.AuthenticateAsync(
				new WebAuthenticatorOptions
				{
					Url = authUri,
#pragma warning disable S1075 // Hardcoded URI
					CallbackUrl = new Uri("http://localhost"),
#pragma warning restore S1075 // Hardcoded URI
					PrefersEphemeralWebBrowserSession = true
				});

			// Reconstruct the full callback URL with query parameters
			var uriBuilder = new UriBuilder(result.Properties["url"]);
			return new Uri(uriBuilder.ToString());
		}
		catch (Exception ex)
		{
			Trace.TraceError($"OAuth browser flow failed: {ex.Message}");
			throw;
		}
	}
}
