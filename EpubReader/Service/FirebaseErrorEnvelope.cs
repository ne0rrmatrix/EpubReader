using System.Text.Json.Serialization;

namespace EpubReader.Service;

sealed class FirebaseErrorEnvelope
{
	[JsonPropertyName("error")]
	public FirebaseErrorBody? Error { get; set; }
}