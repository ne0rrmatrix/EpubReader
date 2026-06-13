using System.Text.Json.Serialization;

namespace EpubReader.Firebase;

sealed class FirebaseErrorBody
{
	[JsonPropertyName("message")]
	public string? Message { get; set; }
}