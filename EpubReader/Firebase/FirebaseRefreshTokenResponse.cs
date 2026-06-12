using System.Text.Json.Serialization;

namespace EpubReader.Firebase;

sealed class FirebaseRefreshTokenResponse
{
	[JsonPropertyName("id_token")]
	public string? IdToken { get; set; }
	[JsonPropertyName("refresh_token")]
	public string? RefreshToken { get; set; }
	[JsonPropertyName("expires_in")]
	public string? ExpiresIn { get; set; }
	[JsonPropertyName("user_id")]
	public string? UserId { get; set; }
}