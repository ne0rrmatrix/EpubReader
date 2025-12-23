namespace EpubReader.Models;

/// <summary>
/// A class representing a font used in an EPUB book.
/// </summary>
public class EpubFonts
{
	/// <summary>
	/// Gets or sets the font family name used for text rendering.
	/// </summary>
	public string FontFamily { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the content as a byte array.
	/// </summary>
	public byte[] Content { get; set; } = [];

	/// <summary>
	/// Gets or sets the name of the file.
	/// </summary>
	public string FileName { get; set; } = string.Empty;
}