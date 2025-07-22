namespace EpubReader.Models;

/// <summary>
/// Represents an OPDS feed containing metadata and entries.
/// </summary>
public class OpdsFeed
{
	public string? Title { get; set; }
	public string? Subtitle { get; set; }
	public string? Id { get; set; }
	public string? Icon { get; set; }
	public DateTime? Updated { get; set; }
	public OpdsAuthor? Author { get; set; }
	public List<OpdsLink> Links { get; set; } = [];
	public List<OpdsEntry> Entries { get; set; } = [];
}
