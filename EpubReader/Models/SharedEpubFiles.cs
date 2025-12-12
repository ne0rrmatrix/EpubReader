namespace EpubReader.Models;

/// <summary>
/// Represents a collection of shared EPUB file data, including file name, HTML content, and binary content.
/// </summary>
public class SharedEpubFiles
{
	/// <summary>
	/// Gets or sets the name of the file.
	/// </summary>
	public string FileName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the HTML content as a string.
	/// </summary>
	public string HTMLContent { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the content as a byte array.
	/// </summary>
	public byte[] Content { get; set; } = [];
}