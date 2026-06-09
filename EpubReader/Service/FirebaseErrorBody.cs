using System.Text.Json.Serialization;

namespace EpubReader.Service;

sealed class FirebaseErrorBody
{
	[JsonPropertyName("message")]
	public string? Message { get; set; }
}