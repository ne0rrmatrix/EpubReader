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

	public static string ApiKey => EnsureValue(GetOrLoad(ref cachedApiKey, FirebaseSecrets.ApiKey), nameof(ApiKey));
	public static string AuthDomain => EnsureValue(GetOrLoad(ref cachedAuthDomain, FirebaseSecrets.AuthDomain), nameof(AuthDomain));
	public static string DatabaseUrl => EnsureValue(GetOrLoad(ref cachedDatabaseUrl, FirebaseSecrets.DatabaseUrl), nameof(DatabaseUrl));

	static string GetOrLoad(ref string? cached, string fallback)
	{
		TryLoadFromGoogleServicesJson();
		cached = !string.IsNullOrWhiteSpace(cached) ? cached : fallback;
		return cached;
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
			using var stream = OpenGoogleServicesStream();
			if (stream is null)
			{
				Trace.TraceWarning("Firebase config: google-services.json not found in app package; falling back to secrets/env.");
				return;
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
			Trace.TraceWarning($"Firebase config: failed to read google-services.json - {ex.Message}");
		}
	}

	static Stream? OpenGoogleServicesStream()
	{
		try
		{
			return FileSystem.OpenAppPackageFileAsync(googleServicesFileName).GetAwaiter().GetResult();
		}
		catch
		{
			// Ignore and attempt filesystem fallback.
		}

		try
		{
			var baseDir = AppContext.BaseDirectory;
			var candidatePaths = new[]
			{
				Path.Combine(baseDir, googleServicesFileName),
				Path.Combine(baseDir, "Resources", googleServicesFileName)
			};

			var filePath = candidatePaths.FirstOrDefault(File.Exists);
			return filePath is not null ? File.OpenRead(filePath) : null;
		}
		catch
		{
			return null;
		}
	}

	static string EnsureValue(string value, string name)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			Trace.TraceWarning($"Firebase configuration '{name}' is missing. Set FIREBASE_API_KEY, FIREBASE_AUTH_DOMAIN, FIREBASE_DATABASE_URL or include google-services.json.");
			return string.Empty;
		}

		return value;
	}
}