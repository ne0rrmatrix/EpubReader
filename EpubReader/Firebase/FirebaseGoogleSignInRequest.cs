using System.Text.Json.Serialization;

namespace EpubReader.Firebase;

sealed class FirebaseGoogleSignInRequest
{
	[JsonPropertyName("postBody")]
	public string PostBody { get; set; } = string.Empty;
	[JsonPropertyName("requestUri")]
	public string RequestUri { get; set; } = string.Empty;
	[JsonPropertyName("returnIdpCredential")]
	public bool ReturnIdpCredential { get; set; }
	[JsonPropertyName("returnSecureToken")]
	public bool ReturnSecureToken { get; set; }
}