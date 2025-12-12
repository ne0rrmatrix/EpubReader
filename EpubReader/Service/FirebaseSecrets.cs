namespace EpubReader.Service;

using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;

// Provides Firebase secret values. Uses generated values when available, otherwise falls back to embedded props or environment variables.
static class FirebaseSecrets
{
	const string generatedTypeName = "EpubReader.Service.FirebaseSecretsGenerated";

	static readonly Lazy<(string ApiKey, string AuthDomain, string DatabaseUrl)> embeddedProps = new(LoadEmbeddedProps);

	internal static string ApiKey => TryGetGenerated(nameof(ApiKey)) ?? embeddedProps.Value.ApiKey ?? Environment.GetEnvironmentVariable("FIREBASE_API_KEY") ?? string.Empty;
	internal static string AuthDomain => TryGetGenerated(nameof(AuthDomain)) ?? embeddedProps.Value.AuthDomain ?? Environment.GetEnvironmentVariable("FIREBASE_AUTH_DOMAIN") ?? string.Empty;
	internal static string DatabaseUrl => TryGetGenerated(nameof(DatabaseUrl)) ?? embeddedProps.Value.DatabaseUrl ?? Environment.GetEnvironmentVariable("FIREBASE_DATABASE_URL") ?? string.Empty;

	static string? TryGetGenerated(string name)
	{
		Type? generatedType = Type.GetType(generatedTypeName);
		FieldInfo? field = generatedType?.GetField(name, BindingFlags.Public | BindingFlags.Static);
		if (field?.GetValue(null) is string value)
		{
			return value;
		}

		return null;
	}

	static (string ApiKey, string AuthDomain, string DatabaseUrl) LoadEmbeddedProps()
	{
		try
		{
			var assembly = typeof(FirebaseSecrets).Assembly;
			var resourceName = assembly
				.GetManifestResourceNames()
				.FirstOrDefault(n => n.EndsWith("firebase.secrets.props", StringComparison.OrdinalIgnoreCase));

			if (resourceName is null)
			{
				return (string.Empty, string.Empty, string.Empty);
			}

			using var stream = assembly.GetManifestResourceStream(resourceName);
			if (stream is null)
			{
				return (string.Empty, string.Empty, string.Empty);
			}

			var document = XDocument.Load(stream);
			var props = document.Root?
				.Element("PropertyGroup");

			if (props is null)
			{
				return (string.Empty, string.Empty, string.Empty);
			}

			return (
				props.Element("FirebaseApiKey")?.Value ?? string.Empty,
				props.Element("FirebaseAuthDomain")?.Value ?? string.Empty,
				props.Element("FirebaseDatabaseUrl")?.Value ?? string.Empty);
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"Firebase secrets: failed to read embedded props - {ex.Message}");
			return (string.Empty, string.Empty, string.Empty);
		}
	}
}