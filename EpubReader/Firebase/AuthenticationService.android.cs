using System.Diagnostics;
using AndroidX.Credentials;
using Firebase.Auth;
using Java.Util.Concurrent;
using Xamarin.GoogleAndroid.Libraries.Identity.GoogleId;

namespace EpubReader.Firebase;

public partial class AuthenticationService : IAuthentication
{
	public async Task<string> SignInWithGooglePlatformAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (Platform.CurrentActivity is not Android.App.Activity activity)
		{
			Trace.TraceError("Google sign-in: current Activity is null");
			throw new InvalidOperationException("Current Activity is null");
		}

		string idToken = await GetGoogleIdTokenAsync(activity, cancellationToken);
		Trace.TraceInformation(string.IsNullOrEmpty(idToken)
			? "Google sign-in: Google ID token retrieval returned empty"
			: "Google sign-in: Google ID token retrieved");
		if (string.IsNullOrEmpty(idToken))
		{
			Trace.TraceWarning("Google sign-in: completed without token/credential");
			return string.Empty;
		}

		Trace.TraceInformation("Google sign-in: IdToken retrieved, exchanging with Firebase");
		FirebaseAuth auth = global::Firebase.Auth.FirebaseAuth.Instance;
		AuthCredential? authCredential = global::Firebase.Auth.GoogleAuthProvider.GetCredential(idToken, null);
		if (authCredential is null)
		{
			Trace.TraceError("Google sign-in: failed to create Firebase auth credential from ID token");
			throw new InvalidOperationException("Failed to create Firebase auth credential from ID token");
		}

		IAuthResult credential = await auth.SignInWithCredentialAsync(authCredential);
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

		string serverClientId = FirebaseConfig.DefaultWebClientId;
		GetGoogleIdOption.Builder googleOptionBuilder = new();
		googleOptionBuilder.SetFilterByAuthorizedAccounts(false);
		googleOptionBuilder.SetAutoSelectEnabled(false);
		if (!string.IsNullOrEmpty(serverClientId))
		{
			googleOptionBuilder.SetServerClientId(serverClientId);
		}

		GetGoogleIdOption googleOption = googleOptionBuilder.Build();
		GetCredentialRequest.Builder requestBuilder = new();
		requestBuilder.AddCredentialOption(googleOption);
		GetCredentialRequest request = requestBuilder.Build();

		try
		{
			ICredentialManager credentialManager = CredentialManager.Create(activity);
			Trace.TraceInformation("Google sign-in: invoking CredentialManager.GetCredentialAsync");

			TaskCompletionSource<(Java.Lang.Object? Result, Java.Lang.Object? Error)> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
			using Android.OS.CancellationSignal cancellationSignal = new();
			using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(cancellationSignal.Cancel);
			using IExecutorService executor = Java.Util.Concurrent.Executors.NewSingleThreadExecutor() ?? throw new InvalidOperationException("CredentialManager executor creation failed");
			using CredentialCallback callback = new(
				onResult: result => completionSource.TrySetResult((result, null)),
				onError: error =>
				{
					completionSource.TrySetResult((null, error));
				});

			credentialManager.GetCredentialAsync(activity, request, cancellationSignal, executor, callback);

			(Java.Lang.Object? resultObject, Java.Lang.Object? errorObject) = await completionSource.Task;
			string errorText = errorObject?.ToString() ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(errorText) &&
				(errorText.Contains("GetCredentialCancellationException", StringComparison.Ordinal) ||
				errorText.Contains("cancelled", StringComparison.OrdinalIgnoreCase) ||
				errorText.Contains("canceled", StringComparison.OrdinalIgnoreCase)))
			{
				Trace.TraceWarning($"Google sign-in: credential request cancelled - {errorText}");
				throw new OperationCanceledException("Google sign-in was cancelled.", cancellationToken);
			}

			if (!string.IsNullOrWhiteSpace(errorText) &&
				(errorText.Contains("NoCredentialException", StringComparison.Ordinal) ||
				errorText.Contains("No credentials available", StringComparison.OrdinalIgnoreCase)))
			{
				Trace.TraceWarning($"Google sign-in: no eligible Google credentials - {errorText}");
				throw new InvalidOperationException("No Google credentials are available on this device. Add a Google account to the emulator or use a device with Google Play services.");
			}

			if (!string.IsNullOrWhiteSpace(errorText))
			{
				Trace.TraceError($"Google sign-in: credential request failed - {errorText}");
				throw new InvalidOperationException($"Google sign-in failed to access device credentials: {errorText}");
			}

			GetCredentialResponse? response = resultObject as GetCredentialResponse;
			Credential? credential = response?.Credential;
			string? type = credential?.Type;
			Android.OS.Bundle? data = credential?.Data;
			if (type is not null && data is not null &&
				(type.Equals(GoogleIdTokenCredential.TypeGoogleIdTokenCredential) || type.Equals(GoogleIdTokenCredential.TypeGoogleIdTokenSiwgCredential)))
			{
				try
				{
					GoogleIdTokenCredential googleIdTokenCredential = GoogleIdTokenCredential.CreateFrom(data);
					string? idToken = googleIdTokenCredential?.IdToken;
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