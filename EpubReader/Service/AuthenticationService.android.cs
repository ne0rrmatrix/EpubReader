using System.Diagnostics;
using Android.App;
using Android.Content;
using Android.Gms.Auth.Api.SignIn;
using Firebase.Auth.Providers;

namespace EpubReader.Service;

public partial class AuthenticationService
{
	public async Task<string> SignInWithGooglePlatformAsync(CancellationToken cancellationToken)
	{
		// Use native Android Google Sign-In to obtain ID token, then exchange with FirebaseAuthClient
		if (Platform.CurrentActivity is not Android.App.Activity activity)
		{
			Trace.TraceError("Google sign-in: current Activity is null");
			throw new InvalidOperationException("Current Activity is null");
		}

		var googleAccount = await GetGoogleSignInAccountAsync(activity).ConfigureAwait(false);
		Trace.TraceInformation(googleAccount is null
			? "Google sign-in: Google account task returned null"
			: "Google sign-in: Google account task returned data");

		if (googleAccount != null && !string.IsNullOrEmpty(googleAccount.IdToken))
		{
			Trace.TraceInformation("Google sign-in: IdToken retrieved, exchanging with Firebase");
			var idToken = googleAccount.IdToken;
			// Exchange ID token with Firebase via FirebaseAuthClient
			var authCredential = GoogleProvider.GetCredential(idToken, OAuthCredentialTokenType.IdToken);
			var credential = await firebaseAuthClient!.SignInWithCredentialAsync(authCredential).ConfigureAwait(false);

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
	}

	Task<GoogleSignInAccount?> GetGoogleSignInAccountAsync(Android.App.Activity activity)
	{
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

		return tcs.Task;
	}
}
