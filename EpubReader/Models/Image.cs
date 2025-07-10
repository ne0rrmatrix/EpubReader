namespace EpubReader.Models;

/// <summary>
/// Represents an image with a file name and binary content.
/// </summary>
public class Image
{
	/// <summary>
	/// Gets or sets the name of the file.
	/// </summary>
	public string FileName { get; set; } = string.Empty;
	
	/// <summary>
	/// Gets or sets the content as a byte array.
	/// </summary>
	public byte[] Content { get; set; } = [];
}
