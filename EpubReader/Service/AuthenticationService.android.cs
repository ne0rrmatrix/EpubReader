using System.Diagnostics;
using AndroidX.Credentials;
using Firebase.Auth.Providers;
using Xamarin.GoogleAndroid.Libraries.Identity.GoogleId;

namespace EpubReader.Service;

public partial class AuthenticationService
{
	public async Task<string> SignInWithGooglePlatformAsync(CancellationToken cancellationToken)
	{
		// Use AndroidX Credential Manager + Google ID to obtain ID token, then exchange with FirebaseAuthClient
		if (Platform.CurrentActivity is not Android.App.Activity activity)
		{
			Trace.TraceError("Google sign-in: current Activity is null");
			throw new InvalidOperationException("Current Activity is null");
		}

		var idToken = await GetGoogleIdTokenAsync(activity, cancellationToken).ConfigureAwait(false);
		Trace.TraceInformation(string.IsNullOrEmpty(idToken)
			? "Google sign-in: Google ID token retrieval returned empty"
			: "Google sign-in: Google ID token retrieved");

		if (!string.IsNullOrEmpty(idToken))
		{
			Trace.TraceInformation("Google sign-in: IdToken retrieved, exchanging with Firebase");
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

	static async Task<string> GetGoogleIdTokenAsync(Android.App.Activity activity, CancellationToken cancellationToken)
	{
		// Discover web client ID from resources if present (Firebase default_web_client_id)
		var clientIdResId = activity.Resources?.GetIdentifier("default_web_client_id", "string", activity.PackageName) ?? 0;
		var serverClientId = clientIdResId != 0 ? activity.GetString(clientIdResId) : string.Empty;
	
		// Build Google GetGoogleIdOption (requests a Google ID token via Credential Manager)
		var googleOptionBuilder = new GetGoogleIdOption.Builder();
		googleOptionBuilder.SetFilterByAuthorizedAccounts(false);
		googleOptionBuilder.SetAutoSelectEnabled(false);
		if (!string.IsNullOrEmpty(serverClientId))
		{
			googleOptionBuilder.SetServerClientId(serverClientId);
		}
		var googleOption = googleOptionBuilder.Build();

		// Build GetCredentialRequest
		var requestBuilder = new GetCredentialRequest.Builder();
		requestBuilder.AddCredentialOption(googleOption);
		var request = requestBuilder.Build();

		try
		{
			var credentialManager = CredentialManager.Create(activity);
			Trace.TraceInformation("Google sign-in: invoking CredentialManager.GetCredentialAsync (callback)");

			var tcs = new TaskCompletionSource<Java.Lang.Object?>(TaskCreationOptions.RunContinuationsAsynchronously);

			var cancellationSignal = new Android.OS.CancellationSignal();
			if (cancellationToken.CanBeCanceled)
			{
				cancellationToken.Register(() =>
				{
					// Cancel the Android cancellation signal when .NET token cancels
					cancellationSignal.Cancel();
				});
			}

			var executor = Java.Util.Concurrent.Executors.NewSingleThreadExecutor() ?? throw new InvalidOperationException("CredentialManager executor creation failed");
			var callback = new CredentialCallback(
				onResult: result => tcs.TrySetResult(result),
				onError: error =>
				{
					Trace.TraceError("Google sign-in: Credential error");
					tcs.TrySetResult(null);
				});

			credentialManager.GetCredentialAsync(activity, request, cancellationSignal, executor, callback);

			var resultObj = await tcs.Task.ConfigureAwait(false);
			var response = resultObj as GetCredentialResponse;
			var cred = response?.Credential;
			var type = cred?.Type;
			var data = cred?.Data;
			if (type is not null && data is not null &&
				(type.Equals(GoogleIdTokenCredential.TypeGoogleIdTokenCredential) || type.Equals(GoogleIdTokenCredential.TypeGoogleIdTokenSiwgCredential)))
			{
				try
				{
					var googleIdTokenCredential = GoogleIdTokenCredential.CreateFrom(data);
					var idToken = googleIdTokenCredential?.IdToken;
					Trace.TraceInformation(string.IsNullOrEmpty(idToken)
						? "Google sign-in: GoogleIdTokenCredential returned empty token"
						: "Google sign-in: GoogleIdTokenCredential returned token");
					return idToken ?? string.Empty;
				}
				catch (GoogleIdTokenParsingException)
				{
					Trace.TraceError("Google sign-in: failed to parse GoogleIdTokenCredential from bundle");
					return string.Empty;
				}
			}

			Trace.TraceWarning("Google sign-in: CredentialManager returned non-GoogleIdTokenCredential or null");
			return string.Empty;
		}
		catch (Java.Lang.Exception jex)
		{
			Trace.TraceError($"Google sign-in: CredentialManager Java exception - {jex.Message}");
			return string.Empty;
		}
		catch (OperationCanceledException)
		{
			Trace.TraceWarning("Google sign-in: operation canceled");
			return string.Empty;
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Google sign-in: CredentialManager error - {ex.Message}");
			return string.Empty;
		}
	}
}

class CredentialCallback(Action<Java.Lang.Object?> onResult, Action<Java.Lang.Object?> onError) : Java.Lang.Object, ICredentialManagerCallback
{
	readonly Action<Java.Lang.Object?> onResult = onResult;
	readonly Action<Java.Lang.Object?> onError = onError;

	public void OnResult(Java.Lang.Object? result) => onResult(result);
	public void OnError(Java.Lang.Object e) => onError(e);
}
