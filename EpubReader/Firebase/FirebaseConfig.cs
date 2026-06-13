using System.Diagnostics;
using System.Text.Json;

namespace EpubReader.Firebase;
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
			using Stream stream = FileSystem.OpenAppPackageFileAsync(googleServicesFileName).GetAwaiter().GetResult() ?? throw new InvalidOperationException($"Required file '{googleServicesFileName}' not found in app package. This application requires build-secrets/{googleServicesFileName} to be present and packaged.");
			using JsonDocument document = JsonDocument.Parse(stream);
			JsonElement root = document.RootElement;

			ParseClient(root);
			ParseProjectInfo(root);
			ParseMeasurementId(root);

			Trace.TraceInformation("Firebase config: loaded from google-services.json");
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Firebase config: failed to read google-services.json - {ex.Message}");
			throw;
		}
	}

	static void ParseClient(JsonElement root)
	{
		if (!root.TryGetProperty("client", out JsonElement clientArray) || clientArray.ValueKind != JsonValueKind.Array || clientArray.GetArrayLength() == 0)
		{
			return;
		}

		JsonElement client = clientArray[0];
		ParseApiKeys(client);
		ParseClientInfo(client);
		ParseOauthClients(client);
	}

	static void ParseApiKeys(JsonElement client)
	{
		if (!client.TryGetProperty("api_key", out JsonElement apiKeys) || apiKeys.ValueKind != JsonValueKind.Array || apiKeys.GetArrayLength() == 0)
		{
			return;
		}

		JsonElement el = apiKeys[0];
		if (el.TryGetProperty("current_key", out JsonElement currentKey) && currentKey.ValueKind == JsonValueKind.String)
		{
			cachedApiKey = currentKey.GetString();
		}
	}

	static void ParseClientInfo(JsonElement client)
	{
		if (!client.TryGetProperty("client_info", out JsonElement clientInfo) || clientInfo.ValueKind != JsonValueKind.Object)
		{
			return;
		}

		if (clientInfo.TryGetProperty("mobilesdk_app_id", out JsonElement appIdEl) && appIdEl.ValueKind == JsonValueKind.String)
		{
			cachedAppId = appIdEl.GetString();
		}

		if (clientInfo.TryGetProperty("android_client_info", out JsonElement androidInfo) && androidInfo.ValueKind == JsonValueKind.Object && androidInfo.TryGetProperty("package_name", out JsonElement pkgEl) && pkgEl.ValueKind == JsonValueKind.String)
		{
			cachedPackageName = pkgEl.GetString();
		}
	}

	static void ParseOauthClients(JsonElement client)
	{
		if (!client.TryGetProperty("oauth_client", out JsonElement oauthClients) || oauthClients.ValueKind != JsonValueKind.Array)
		{
			return;
		}

		foreach (JsonElement oauth in oauthClients.EnumerateArray())
		{
			if (!oauth.TryGetProperty("client_type", out JsonElement clientType) || clientType.ValueKind != JsonValueKind.Number || clientType.GetInt32() != 3)
			{
				continue;
			}

			if (oauth.TryGetProperty("client_id", out JsonElement cid) && cid.ValueKind == JsonValueKind.String)
			{
				cachedDefaultWebClientId = cid.GetString();
				break;
			}
		}
	}

	static void ParseProjectInfo(JsonElement root)
	{
		if (!root.TryGetProperty("project_info", out JsonElement projectInfo) || projectInfo.ValueKind != JsonValueKind.Object)
		{
			return;
		}

		if (projectInfo.TryGetProperty("project_id", out JsonElement pid) && pid.ValueKind == JsonValueKind.String)
		{
			cachedProjectId = pid.GetString();
			if (!string.IsNullOrWhiteSpace(cachedProjectId) && string.IsNullOrWhiteSpace(cachedAuthDomain))
			{
				cachedAuthDomain = $"{cachedProjectId}.firebaseapp.com";
			}
		}

		if (projectInfo.TryGetProperty("firebase_url", out JsonElement furl) && furl.ValueKind == JsonValueKind.String)
		{
			cachedDatabaseUrl = furl.GetString();
		}

		if (projectInfo.TryGetProperty("project_number", out JsonElement pnum) && (pnum.ValueKind == JsonValueKind.String || pnum.ValueKind == JsonValueKind.Number))
		{
			cachedProjectNumber = pnum.GetString();
		}

		if (projectInfo.TryGetProperty("storage_bucket", out JsonElement sb) && sb.ValueKind == JsonValueKind.String)
		{
			cachedStorageBucket = sb.GetString();
		}
	}

	static void ParseMeasurementId(JsonElement root)
	{
		if (root.TryGetProperty("measurement_id", out JsonElement mid) && mid.ValueKind == JsonValueKind.String)
		{
			cachedMeasurementId = mid.GetString();
		}
	}
}