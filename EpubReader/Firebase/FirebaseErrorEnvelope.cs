using System.Text.Json.Serialization;

namespace EpubReader.Firebase;

sealed class FirebaseErrorEnvelope
{
	[JsonPropertyName("error")]
	public FirebaseErrorBody? Error { get; set; }
}