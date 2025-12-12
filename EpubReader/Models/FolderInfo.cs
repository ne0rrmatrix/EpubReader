namespace EpubReader.Models;

/// <summary>
/// Represents information about a folder, including its title and item count.
/// </summary>
public class FolderInfo
{
	/// <summary>
	/// Gets or sets the count of items.
	/// </summary>
	public int Count { get; set; } = 0;

	/// <summary>
	/// Gets or sets the title of the item.
	/// </summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the maximum count allowed for the operation.
	/// </summary>
	public int MaxCount { get; set; } = 0;
}