namespace EpubReader.Models;

/// <summary>
/// Represents an author in an OPDS feed or entry.
/// </summary>
public class OpdsAuthor
{
	/// <summary>
	/// Gets or sets the name associated with the entity.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the URI associated with the entity.
	/// </summary>
	public string? Uri { get; set; }
}