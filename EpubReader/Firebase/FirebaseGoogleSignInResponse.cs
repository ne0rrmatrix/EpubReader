using System.Text.Json.Serialization;

namespace EpubReader.Firebase;

sealed class FirebaseGoogleSignInResponse
{
	[JsonPropertyName("localId")]
	public string LocalId { get; set; } = string.Empty;
	[JsonPropertyName("email")]
	public string? Email { get; set; }
	[JsonPropertyName("idToken")]
	public string? IdToken { get; set; }
	[JsonPropertyName("refreshToken")]
	public string? RefreshToken { get; set; }
	[JsonPropertyName("expiresIn")]
	public string? ExpiresIn { get; set; }
}