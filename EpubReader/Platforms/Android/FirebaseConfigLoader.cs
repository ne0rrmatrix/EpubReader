#if ANDROID
using System.Text.Json;
using System.IO;

namespace EpubReader.Platforms.Android;

public static class FirebaseConfigLoader
{
	const string googleApiKeyKey = "google_api_key";
	const string googleAppIdKey = "google_app_id";
	const string defaultWebClientIdKey = "default_web_client_id";
	const string firebaseAuthDomainKey = "firebase_auth_domain";
	const string firebaseDatabaseUrlKey = "firebase_database_url";

	/// <summary>
	/// Loads Firebase configuration from environment variables, assets, or preferences (in that order).
	/// </summary>
	public static void InjectFirebaseSecrets()
	{
		try
		{
			// Next try Resources/raw (packaged resource) - supports older layout where google-services.json was placed in Resources/Raw
			if (TryLoadFromRawResource())
			{
				System.Diagnostics.Debug.WriteLine("✓ Firebase secrets loaded from Resources/raw google-services.json");
				return;
			}

			System.Diagnostics.Debug.WriteLine("✗ google-services.json not found in resources, assets, or app files. Sign-in may not work until google-services.json (build-secrets) is provided at build or runtime.");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"✗ Failed to load Firebase secrets from asset: {ex.Message}");
			throw;
		}
	}

	static bool TryLoadFromAsset()
	{
		try
		{
			var context = global::Android.App.Application.Context;
			if (context is null)
			{
				return false;
			}

			var assets = context.Assets;
			if (assets is null)
			{
				return false;
			}

			var assetNames = assets.List("") ?? Array.Empty<string>();
			// Support either a top-level asset or a build-secrets subpath (we set a distinct LogicalName
			// for the canonical secrets file to avoid publish collisions). Look for either location.
			var assetName = assetNames.Contains("google-services.json") ? "google-services.json" :
							(assetNames.Contains("build-secrets") && (assets.List("build-secrets") ?? []).Contains("google-services.json") ? "build-secrets/google-services.json" : null);

			if (assetName is null)
			{
				return false;
			}

			using var assetStream = assets.Open(assetName);
			using var reader = new StreamReader(assetStream);
			var json = reader.ReadToEnd();

			var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			var (appId, apiKey, firebaseUrl, authDomain, webClientId) = ExtractFirebaseConfig(root);

			return !string.IsNullOrWhiteSpace(appId);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"⚠ Failed to load from google-services.json asset: {ex.Message}");
			return false;
		}
	}

	static bool TryLoadFromRawResource()
	{
		try
		{
			var context = global::Android.App.Application.Context;
			if (context is null)
			{
				return false;
			}

			// Candidates: resource names derived from possible file names
			var resources = context.Resources;
			if (resources is null)
			{
				return false;
			}
			var candidates = new[] { "google_services", "google_service", "google_services_json", "google_service_json" };
			foreach (var name in candidates)
			{
				var id = resources.GetIdentifier(name, "raw", context.PackageName);
				if (id != 0)
				{
					using var stream = resources.OpenRawResource(id);
					using var reader = new StreamReader(stream);
					var json = reader.ReadToEnd();
					var doc = JsonDocument.Parse(json);
					var root = doc.RootElement;
					var (appId, apiKey, firebaseUrl, authDomain, webClientId) = ExtractFirebaseConfig(root);


					return !string.IsNullOrWhiteSpace(appId);
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"⚠ Failed to load google-services.json from Resources/raw: {ex.Message}");
		}
		return false;
	}

	static bool TryLoadFromFilesDir()
	{
		try
		{
			var context = global::Android.App.Application.Context;
			if (context is null)
			{
				return false;
			}
			var filesDir = context.FilesDir?.AbsolutePath ?? string.Empty;
			if (string.IsNullOrWhiteSpace(filesDir))
			{
				return false;
			}
			var candidates = new[] {
				Path.Combine(filesDir, "build-secrets", "google-services.json"),
				Path.Combine(filesDir, "google-services.json")
			};
			foreach (var p in candidates)
			{
				if (File.Exists(p))
				{
					var json = File.ReadAllText(p);
					var doc = JsonDocument.Parse(json);
					var root = doc.RootElement;
					var (appId, apiKey, firebaseUrl, authDomain, webClientId) = ExtractFirebaseConfig(root);


					return !string.IsNullOrWhiteSpace(appId);
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"⚠ Failed to load google-services.json from files dir: {ex.Message}");
		}
		return false;
	}


	static (string? appId, string? apiKey, string? firebaseUrl, string? authDomain, string? webClientId) ExtractFirebaseConfig(JsonElement root)
	{
		string? appId = null;
		string? apiKey = null;
		string? firebaseUrl = null;
		string? projectId = null;
		string webClientId = string.Empty;

		if (root.TryGetProperty("client", out var clientArray) && clientArray.GetArrayLength() > 0)
		{
			var client = clientArray[0];

			appId = TryGetAppId(client);
			apiKey = TryGetApiKey(client);
			webClientId = TryGetWebClientId(client);
		}

		if (root.TryGetProperty("project_info", out var projectInfo) && projectInfo.ValueKind == JsonValueKind.Object)
		{
			projectId = projectInfo.TryGetProperty("project_id", out var pid) ? pid.GetString() : null;
			firebaseUrl = projectInfo.TryGetProperty("firebase_url", out var furl) ? furl.GetString() : null;
		}

		var authDomain = !string.IsNullOrWhiteSpace(projectId) ? $"{projectId}.firebaseapp.com" : null;

		return (appId, apiKey, firebaseUrl, authDomain, webClientId);
	}

	static string? TryGetAppId(JsonElement client)
	{
		if (client.TryGetProperty("client_info", out var clientInfo) && clientInfo.ValueKind == JsonValueKind.Object && clientInfo.TryGetProperty("mobilesdk_app_id", out var appIdElement))
		{
			return appIdElement.GetString();
		}
		return null;
	}

	static string? TryGetApiKey(JsonElement client)
	{
		if (client.TryGetProperty("api_key", out var apiKeys) && apiKeys.GetArrayLength() > 0)
		{
			return apiKeys[0].GetProperty("current_key").GetString();
		}
		return null;
	}

	static string TryGetWebClientId(JsonElement client)
	{
		if (client.TryGetProperty("oauth_client", out var oauthClientsElement) && oauthClientsElement.ValueKind == JsonValueKind.Array)
		{
			foreach (var oauthClient in oauthClientsElement.EnumerateArray())
			{
				if (oauthClient.TryGetProperty("client_type", out var clientType) && clientType.GetInt32() == 3)
				{
					return oauthClient.TryGetProperty("client_id", out var cid) ? cid.GetString() ?? string.Empty : string.Empty;
				}
			}
		}
		return string.Empty;
	}

	public static string GetConfigValue(string key, string defaultValue = "")
	{
		try
		{
			var context = global::Android.App.Application.Context;
			if (context is null)
			{
				return defaultValue;
			}

			// 1) Resources/raw
			var resources = context.Resources;
			if (resources is not null)
			{
				var rawCandidates = new[] { "google_services", "google_service", "google_services_json", "google_service_json" };
				foreach (var name in rawCandidates)
				{
					var id = resources.GetIdentifier(name, "raw", context.PackageName);
					if (id != 0)
					{
						using var stream = resources.OpenRawResource(id);
						using var reader = new StreamReader(stream);
						var json = reader.ReadToEnd();
						var root = JsonDocument.Parse(json).RootElement;
						var (appId, apiKey, firebaseUrl, authDomain, webClientId) = ExtractFirebaseConfig(root);
						return key switch
						{
							var k when k == googleApiKeyKey => apiKey ?? defaultValue,
							var k when k == googleAppIdKey => appId ?? defaultValue,
							var k when k == firebaseDatabaseUrlKey => firebaseUrl ?? defaultValue,
							var k when k == firebaseAuthDomainKey => authDomain ?? defaultValue,
							var k when k == defaultWebClientIdKey => webClientId ?? defaultValue,
							_ => defaultValue
						};
					}
				}
			}

			// 2) Assets
			var assets = context.Assets;
			if (assets is not null)
			{
				var assetNames = assets.List("") ?? Array.Empty<string>();
				var assetName = assetNames.Contains("google-services.json") ? "google-services.json" :
								(assetNames.Contains("build-secrets") && (assets.List("build-secrets") ?? Array.Empty<string>()).Contains("google-services.json") ? "build-secrets/google-services.json" : null);
				if (assetName is not null)
				{
					using var assetStream = assets.Open(assetName);
					using var reader = new StreamReader(assetStream);
					var json = reader.ReadToEnd();
					var root = JsonDocument.Parse(json).RootElement;
					var (appId, apiKey, firebaseUrl, authDomain, webClientId) = ExtractFirebaseConfig(root);
					return key switch
					{
						var k when k == googleApiKeyKey => apiKey ?? defaultValue,
						var k when k == googleAppIdKey => appId ?? defaultValue,
						var k when k == firebaseDatabaseUrlKey => firebaseUrl ?? defaultValue,
						var k when k == firebaseAuthDomainKey => authDomain ?? defaultValue,
						var k when k == defaultWebClientIdKey => webClientId ?? defaultValue,
						_ => defaultValue
					};
				}
			}

			// 3) Files dir
			var filesDir = context.FilesDir?.AbsolutePath ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(filesDir))
			{
				var candidates = new[] { Path.Combine(filesDir, "build-secrets", "google-services.json"), Path.Combine(filesDir, "google-services.json") };
				foreach (var p in candidates)
				{
					if (File.Exists(p))
					{
						var json = File.ReadAllText(p);
						var root = JsonDocument.Parse(json).RootElement;
						var (appId, apiKey, firebaseUrl, authDomain, webClientId) = ExtractFirebaseConfig(root);
						return key switch
						{
							var k when k == googleApiKeyKey => apiKey ?? defaultValue,
							var k when k == googleAppIdKey => appId ?? defaultValue,
							var k when k == firebaseDatabaseUrlKey => firebaseUrl ?? defaultValue,
							var k when k == firebaseAuthDomainKey => authDomain ?? defaultValue,
							var k when k == defaultWebClientIdKey => webClientId ?? defaultValue,
							_ => defaultValue
						};
					}
				}
			}
		}
		catch
		{
			// ignore and fallthrough to default
		}
		return defaultValue;
	}

	public static bool IsConfigValid()
	{
		var appId = GetConfigValue(googleAppIdKey);
		return !string.IsNullOrWhiteSpace(appId) && !appId.Contains("REPLACE_WITH_LOCAL_SECRET");
	}
}

#endif