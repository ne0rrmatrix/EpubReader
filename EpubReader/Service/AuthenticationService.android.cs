using System.Diagnostics;
using AndroidX.Credentials;
using Microsoft.Maui.ApplicationModel;
using Xamarin.GoogleAndroid.Libraries.Identity.GoogleId;

namespace EpubReader.Service;

public partial class AuthenticationService
{
	public async Task<string> SignInWithGooglePlatformAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (Platform.CurrentActivity is not Android.App.Activity activity)
		{
			Trace.TraceError("Google sign-in: current Activity is null");
			throw new InvalidOperationException("Current Activity is null");
		}

		var idToken = await GetGoogleIdTokenAsync(activity, cancellationToken);
		Trace.TraceInformation(string.IsNullOrEmpty(idToken)
			? "Google sign-in: Google ID token retrieval returned empty"
			: "Google sign-in: Google ID token retrieved");
		if (string.IsNullOrEmpty(idToken))
		{
			Trace.TraceWarning("Google sign-in: completed without token/credential");
			return string.Empty;
		}

		Trace.TraceInformation("Google sign-in: IdToken retrieved, exchanging with Firebase");
		var auth = Firebase.Auth.FirebaseAuth.Instance;
		var authCredential = Firebase.Auth.GoogleAuthProvider.GetCredential(idToken, null);
		if (authCredential is null)
		{
			Trace.TraceError("Google sign-in: failed to create Firebase auth credential from ID token");
			throw new InvalidOperationException("Failed to create Firebase auth credential from ID token");
		}

		var credential = await auth.SignInWithCredentialAsync(authCredential);
		if (credential?.User is null)
		{
			Trace.TraceError("Google sign-in: Firebase sign-in with credential returned null user");
			throw new InvalidOperationException("Firebase sign-in with credential returned null user");
		}

		Trace.TraceInformation("Google sign-in: Firebase credential obtained, storing secure data");
		await SecureStorage.SetAsync(userIdKey, credential.User.Uid);
		await SecureStorage.SetAsync(userEmailKey, credential.User.Email ?? string.Empty);
		Preferences.Set(authModeKey, authModeCloud);
		AuthStateChanged?.Invoke(this, true);
		return credential.User.Uid;
	}

	static async Task<string> GetGoogleIdTokenAsync(Android.App.Activity activity, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var serverClientId = FirebaseConfig.DefaultWebClientId;
		var googleOptionBuilder = new GetGoogleIdOption.Builder();
		googleOptionBuilder.SetFilterByAuthorizedAccounts(false);
		googleOptionBuilder.SetAutoSelectEnabled(false);
		if (!string.IsNullOrEmpty(serverClientId))
		{
			googleOptionBuilder.SetServerClientId(serverClientId);
		}

		var googleOption = googleOptionBuilder.Build();
		var requestBuilder = new GetCredentialRequest.Builder();
		requestBuilder.AddCredentialOption(googleOption);
		var request = requestBuilder.Build();

		try
		{
			var credentialManager = CredentialManager.Create(activity);
			Trace.TraceInformation("Google sign-in: invoking CredentialManager.GetCredentialAsync");

			var completionSource = new TaskCompletionSource<Java.Lang.Object?>(TaskCreationOptions.RunContinuationsAsynchronously);
			using var cancellationSignal = new Android.OS.CancellationSignal();
			using var cancellationRegistration = cancellationToken.Register(cancellationSignal.Cancel);
			using var executor = Java.Util.Concurrent.Executors.NewSingleThreadExecutor() ?? throw new InvalidOperationException("CredentialManager executor creation failed");
			using var callback = new CredentialCallback(
				onResult: result => completionSource.TrySetResult(result),
				onError: error =>
				{
					Trace.TraceError($"Google sign-in: Credential error - {error}");
					completionSource.TrySetResult(null);
				});

			credentialManager.GetCredentialAsync(activity, request, cancellationSignal, executor, callback);

			var resultObject = await completionSource.Task;
			var response = resultObject as GetCredentialResponse;
			var credential = response?.Credential;
			var type = credential?.Type;
			var data = credential?.Data;
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
				catch (GoogleIdTokenParsingException ex)
				{
					Trace.TraceError($"Google sign-in: failed to parse GoogleIdTokenCredential from bundle - {ex.Message}");
					return string.Empty;
				}
			}

			Trace.TraceWarning("Google sign-in: CredentialManager returned non-GoogleIdTokenCredential or null");
			return string.Empty;
		}
		catch (OperationCanceledException)
		{
			Trace.TraceWarning("Google sign-in: operation canceled");
			throw;
		}
		catch (Java.Lang.Exception ex)
		{
			Trace.TraceError($"Google sign-in: CredentialManager Java exception - {ex.Message}");
			return string.Empty;
		}
	}
}

sealed class CredentialCallback(Action<Java.Lang.Object?> onResult, Action<Java.Lang.Object?> onError) : Java.Lang.Object, ICredentialManagerCallback
{
	readonly Action<Java.Lang.Object?> onResult = onResult;
	readonly Action<Java.Lang.Object?> onError = onError;

	public void OnResult(Java.Lang.Object? result) => onResult(result);
	public void OnError(Java.Lang.Object e) => onError(e);
}