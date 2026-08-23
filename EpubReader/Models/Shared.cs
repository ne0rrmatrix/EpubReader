namespace EpubReader.Models;

public class Shared
{
	/// <summary>
	/// ISO 8601 UTC timestamp of when the book was added to the library (if syncing metadata).
	/// </summary>
	public string? DateAdded { get; set; }

	/// <summary>
	/// ISO 8601 UTC timestamp of when the book was last opened (if syncing metadata).
	/// </summary>
	public string? LastOpenedDate { get; set; }
}