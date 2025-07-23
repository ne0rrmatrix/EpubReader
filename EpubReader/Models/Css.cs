namespace EpubReader.Models;

/// <summary>
/// Represents a CSS file with its associated file name and content.
/// </summary>
public class Css
{
	/// <summary>
	/// Gets or sets the file name of the CSS file.
	/// </summary>
	/// <remarks>This property is used to identify the CSS file within the EPUB document.</remarks>
	public string FileName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the content as a string.
	/// </summary>
    public string Content { get; set; } = string.Empty;
}
