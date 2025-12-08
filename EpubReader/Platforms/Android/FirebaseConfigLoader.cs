#if ANDROID
using Android.App;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace EpubReader.Platforms.Android;

public static class FirebaseConfigLoader
{
    const string googleApiKeyKey = "google_api_key";
    const string googleAppIdKey = "google_app_id";
    const string firebaseUrlKey = "firebase_url";
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
            // Step 1: Try environment variables (CI/CD priority)
            if (TryLoadFromEnvironmentVariables())
            {
                System.Diagnostics.Debug.WriteLine("✓ Firebase secrets loaded from environment variables");
                return;
            }

            // Step 2: Try google-services.json asset (local dev)
            if (TryLoadFromAsset())
            {
                System.Diagnostics.Debug.WriteLine("✓ Firebase secrets loaded from google-services.json asset");
                return;
            }

            // Step 3: Fall back to Android resources (legacy, will use placeholders if not injected)
            System.Diagnostics.Debug.WriteLine("⚠ Firebase secrets not found in environment or assets. Using Android resources (may contain placeholders).");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"✗ Failed to load Firebase secrets: {ex.Message}");
            // Do not throw here to avoid crashing app initialization; caller can validate via IsConfigValid()
        }
    }

    static bool TryLoadFromEnvironmentVariables()
    {
        var appId = Environment.GetEnvironmentVariable("FIREBASE_APP_ID");
        var apiKey = Environment.GetEnvironmentVariable("FIREBASE_API_KEY");
        var databaseUrl = Environment.GetEnvironmentVariable("FIREBASE_DATABASE_URL");
        var authDomain = Environment.GetEnvironmentVariable("FIREBASE_AUTH_DOMAIN");
        var webClientId = Environment.GetEnvironmentVariable("FIREBASE_WEB_CLIENT_ID");

        // Only succeed if at least the critical app ID is provided
        if (string.IsNullOrWhiteSpace(appId))
        {
            return false;
        }

        Preferences.Set(googleAppIdKey, appId);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            Preferences.Set(googleApiKeyKey, apiKey);
        }

        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            Preferences.Set(firebaseDatabaseUrlKey, databaseUrl);
        }

        if (!string.IsNullOrWhiteSpace(authDomain))
        {
            Preferences.Set(firebaseAuthDomainKey, authDomain);
        }

        if (!string.IsNullOrWhiteSpace(webClientId))
        {
            Preferences.Set(defaultWebClientIdKey, webClientId);
        }

        return true;
    }

    static bool TryLoadFromAsset()
    {
        try
        {
            // qualify Application to avoid ambiguity with Microsoft.Maui.Application
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

            // Check if asset exists
            var assetNames = assets.List("") ?? Array.Empty<string>();
            if (!assetNames.Contains("google-services.json"))
            {
                return false;
            }

            using var assetStream = assets.Open("google-services.json");
            using var reader = new StreamReader(assetStream);
            var json = reader.ReadToEnd();

            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Extract values from google-services.json structure
            if (!root.TryGetProperty("client", out var clientArray) || clientArray.GetArrayLength() == 0)
            {
                return false;
            }

            var client = clientArray[0];

            // client_info may or may not exist; use TryGetProperty
            string? appId = null;
            if (client.TryGetProperty("client_info", out var clientInfo) && clientInfo.ValueKind == JsonValueKind.Object)
            {
                if (clientInfo.TryGetProperty("mobilesdk_app_id", out var appIdElement))
                {
                    appId = appIdElement.GetString();
                }
            }

            string? apiKey = null;
            if (client.TryGetProperty("api_key", out var apiKeys) && apiKeys.GetArrayLength() > 0)
            {
                apiKey = apiKeys[0].GetProperty("current_key").GetString();
            }

            string? firebaseUrl = null;
            string? projectId = null;
            if (root.TryGetProperty("project_info", out var projectInfo) && projectInfo.ValueKind == JsonValueKind.Object)
            {
                projectId = projectInfo.TryGetProperty("project_id", out var pid) ? pid.GetString() : null;
                firebaseUrl = projectInfo.TryGetProperty("firebase_url", out var furl) ? furl.GetString() : null;
            }

            var authDomain = !string.IsNullOrWhiteSpace(projectId) ? $"{projectId}.firebaseapp.com" : null;

            // Find OAuth web client ID (client_type == 3)
            var webClientId = string.Empty;
            if (client.TryGetProperty("oauth_client", out var oauthClientsElement) && oauthClientsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var oauthClient in oauthClientsElement.EnumerateArray())
                {
                    if (oauthClient.TryGetProperty("client_type", out var clientType) && clientType.GetInt32() == 3)
                    {
                        webClientId = oauthClient.TryGetProperty("client_id", out var cid) ? cid.GetString() ?? string.Empty : string.Empty;
                        break;
                    }
                }
            }

            // Store in preferences
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                Preferences.Set(googleApiKeyKey, apiKey);
            }

            if (!string.IsNullOrWhiteSpace(appId))
            {
                Preferences.Set(googleAppIdKey, appId);
            }

            if (!string.IsNullOrWhiteSpace(firebaseUrl))
            {
                Preferences.Set(firebaseDatabaseUrlKey, firebaseUrl);
            }

            if (!string.IsNullOrWhiteSpace(authDomain))
            {
                Preferences.Set(firebaseAuthDomainKey, authDomain);
            }

            if (!string.IsNullOrWhiteSpace(webClientId))
            {
                Preferences.Set(defaultWebClientIdKey, webClientId);
            }

            return !string.IsNullOrWhiteSpace(appId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠ Failed to load from google-services.json asset: {ex.Message}");
            return false;
        }
    }

    public static string GetConfigValue(string key, string defaultValue = "")
    {
        return Preferences.Get(key, defaultValue);
    }

    public static bool IsConfigValid()
    {
        var appId = GetConfigValue(googleAppIdKey);
        return !string.IsNullOrWhiteSpace(appId) && !appId.Contains("REPLACE_WITH_LOCAL_SECRET");
    }
}

#endif