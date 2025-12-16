namespace EpubReader.Service;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Maui.Storage;

/// <summary>
/// Firebase configuration for the application generated at build from google-services.json, environment variables, or MSBuild properties.
/// </summary>
static class FirebaseConfig
{
	const string googleServicesFileName = "google-services.json";
	static bool loadedFromGoogleServices;
	static string? cachedApiKey;
	static string? cachedAuthDomain;
	static string? cachedDatabaseUrl;

	public static string ApiKey => EnsureValue(GetOrLoad(ref cachedApiKey), nameof(ApiKey));
	public static string AuthDomain => EnsureValue(GetOrLoad(ref cachedAuthDomain), nameof(AuthDomain));
	public static string DatabaseUrl => EnsureValue(GetOrLoad(ref cachedDatabaseUrl), nameof(DatabaseUrl));

	static string GetOrLoad(ref string? cached)
	{
		TryLoadFromGoogleServicesJson();
		return cached ?? string.Empty;
	}

	static void TryLoadFromGoogleServicesJson()
	{
		if (loadedFromGoogleServices)
		{
			return;
		}

		loadedFromGoogleServices = true;

		try
		{
			using var stream = FileSystem.OpenAppPackageFileAsync(googleServicesFileName).GetAwaiter().GetResult();
			if (stream is null)
			{
				throw new InvalidOperationException($"Required file '{googleServicesFileName}' not found in app package. This application requires build-secrets/{googleServicesFileName} to be present and packaged.");
			}

			using var document = JsonDocument.Parse(stream);
			var root = document.RootElement;

			cachedApiKey = root
				.GetProperty("client")[0]
				.GetProperty("api_key")[0]
				.GetProperty("current_key")
				.GetString();

			var projectId = root.GetProperty("project_info").GetProperty("project_id").GetString();
			if (!string.IsNullOrWhiteSpace(projectId))
			{
				cachedAuthDomain = $"{projectId}.firebaseapp.com";
			}

			cachedDatabaseUrl = root
				.GetProperty("project_info")
				.GetProperty("firebase_url")
				.GetString();

			Trace.TraceInformation("Firebase config: loaded from google-services.json");
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Firebase config: failed to read google-services.json - {ex.Message}");
			throw;
		}
	}

	static string EnsureValue(string value, string name)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new InvalidOperationException($"Firebase configuration '{name}' is missing. Ensure build-secrets/google-services.json is present and contains the required values.");
		}

		return value;
	}
}