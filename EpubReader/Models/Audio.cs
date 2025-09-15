namespace EpubReader.Models;

/// <summary>
/// Represents an audio file with its associated data.
/// </summary>
public class Audio
{
	/// <summary>
	/// Gets or sets the name of the file.
	/// </summary>
	public string FileName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the byte array representing the data content.
	/// </summary>
	public byte[] Data { get; set; } = [];
}
