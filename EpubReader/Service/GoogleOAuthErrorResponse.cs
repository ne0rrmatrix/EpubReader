using System.Text.Json.Serialization;

namespace EpubReader.Service;

sealed class GoogleOAuthErrorResponse
{
	[JsonPropertyName("error")]
	public string? Error { get; set; }
	[JsonPropertyName("error_description")]
	public string? ErrorDescription { get; set; }
}