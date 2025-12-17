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

	// Additional commonly used values from google-services.json
	static string? cachedAppId;
	static string? cachedDefaultWebClientId;
	static string? cachedProjectId;
	static string? cachedProjectNumber;
	static string? cachedStorageBucket;
	static string? cachedPackageName;
	static string? cachedMeasurementId;

	// Read values strictly from google-services.json (no fallbacks)
	public static string ApiKey => GetOrLoad(ref cachedApiKey);
	public static string AuthDomain => GetOrLoad(ref cachedAuthDomain);
	public static string DatabaseUrl => GetOrLoad(ref cachedDatabaseUrl);

	// Non-mandatory values: return empty string when not present
	public static string AppId => GetOrLoad(ref cachedAppId);
	public static string DefaultWebClientId => GetOrLoad(ref cachedDefaultWebClientId);
	public static string ProjectId => GetOrLoad(ref cachedProjectId);
	public static string ProjectNumber => GetOrLoad(ref cachedProjectNumber);
	public static string StorageBucket => GetOrLoad(ref cachedStorageBucket);
	public static string PackageName => GetOrLoad(ref cachedPackageName);
	public static string MeasurementId => GetOrLoad(ref cachedMeasurementId);

	static string GetOrLoad(ref string? cached)
	{
		TryLoadFromGoogleServicesJson();
		return cached ?? string.Empty;
	}

	public static void TryLoadFromGoogleServicesJson()
	{
		if (loadedFromGoogleServices)
		{
			return;
		}

		loadedFromGoogleServices = true;

		try
		{
			using var stream = FileSystem.OpenAppPackageFileAsync(googleServicesFileName).GetAwaiter().GetResult() ?? throw new InvalidOperationException($"Required file '{googleServicesFileName}' not found in app package. This application requires build-secrets/{googleServicesFileName} to be present and packaged.");
			using var document = JsonDocument.Parse(stream);
			var root = document.RootElement;

			// Parse client array
			if (root.TryGetProperty("client", out var clientArray) && clientArray.ValueKind == JsonValueKind.Array && clientArray.GetArrayLength() > 0)
			{
				var client = clientArray[0];

				// api_key
				if (client.TryGetProperty("api_key", out var apiKeys) && apiKeys.ValueKind == JsonValueKind.Array && apiKeys.GetArrayLength() > 0)
				{
					var el = apiKeys[0];
					if (el.TryGetProperty("current_key", out var currentKey) && currentKey.ValueKind == JsonValueKind.String)
					{
						cachedApiKey = currentKey.GetString();
					}
				}

				// client_info -> mobilesdk_app_id and android package name
				if (client.TryGetProperty("client_info", out var clientInfo) && clientInfo.ValueKind == JsonValueKind.Object)
				{
					if (clientInfo.TryGetProperty("mobilesdk_app_id", out var appIdEl) && appIdEl.ValueKind == JsonValueKind.String)
					{
						cachedAppId = appIdEl.GetString();
					}

					if (clientInfo.TryGetProperty("android_client_info", out var androidInfo) && androidInfo.ValueKind == JsonValueKind.Object && androidInfo.TryGetProperty("package_name", out var pkgEl) && pkgEl.ValueKind == JsonValueKind.String)
					{
						cachedPackageName = pkgEl.GetString();
					}
				}

				// oauth_client -> default web client id (client_type == 3)
				if (client.TryGetProperty("oauth_client", out var oauthClients) && oauthClients.ValueKind == JsonValueKind.Array)
				{
					foreach (var oauth in oauthClients.EnumerateArray())
					{
						if (oauth.TryGetProperty("client_type", out var clientType) && clientType.ValueKind == JsonValueKind.Number && clientType.GetInt32() == 3 && oauth.TryGetProperty("client_id", out var cid) && cid.ValueKind == JsonValueKind.String)
						{
							cachedDefaultWebClientId = cid.GetString();
							break;
						}
					}
				}
			}

			// project_info
			if (root.TryGetProperty("project_info", out var projectInfo) && projectInfo.ValueKind == JsonValueKind.Object)
			{
				if (projectInfo.TryGetProperty("project_id", out var pid) && pid.ValueKind == JsonValueKind.String)
				{
					cachedProjectId = pid.GetString();
					if (!string.IsNullOrWhiteSpace(cachedProjectId) && string.IsNullOrWhiteSpace(cachedAuthDomain))
					{
						cachedAuthDomain = $"{cachedProjectId}.firebaseapp.com";
					}
				}

				if (projectInfo.TryGetProperty("firebase_url", out var furl) && furl.ValueKind == JsonValueKind.String)
				{
					cachedDatabaseUrl = furl.GetString();
				}

				if (projectInfo.TryGetProperty("project_number", out var pnum) && (pnum.ValueKind == JsonValueKind.String || pnum.ValueKind == JsonValueKind.Number))
				{
					cachedProjectNumber = pnum.GetString();
				}

				if (projectInfo.TryGetProperty("storage_bucket", out var sb) && sb.ValueKind == JsonValueKind.String)
				{
					cachedStorageBucket = sb.GetString();
				}
			}

			// measurement_id (top-level)
			if (root.TryGetProperty("measurement_id", out var mid) && mid.ValueKind == JsonValueKind.String)
			{
				cachedMeasurementId = mid.GetString();
			}

			Trace.TraceInformation("Firebase config: loaded from google-services.json");
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Firebase config: failed to read google-services.json - {ex.Message}");
			throw;
		}
	}
}