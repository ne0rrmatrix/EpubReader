namespace EpubReader.Models;

/// <summary>
/// Represents a chapter in a document or book, containing metadata such as the title and associated file paths.
/// </summary>
public class Chapter
{

	/// <summary>
	/// Gets or sets the title of the item.
	/// </summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the HTML file associated with the chapter.
	/// </summary>
	/// <remarks>This property holds the path to the HTML file that contains the chapter's content.</remarks>
	public string HtmlFile { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the name of the file.
	/// </summary>
	public string FileName { get; set; } = string.Empty;
}