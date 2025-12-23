namespace EpubReader.Models;

/// <summary>
/// Represents an individual entry in an OPDS feed.
/// </summary>
public class OpdsEntry
{
	public string? Title { get; set; }
	public string? Id { get; set; }
	public string? Content { get; set; }
	public DateTime? Updated { get; set; }
	public DateTime? Published { get; set; }
	public DateTime? DcDate { get; set; } // Dublin Core date
	public List<OpdsLink> Links { get; set; } = [];
	public List<OpdsAuthor> Authors { get; set; } = [];
	public string? Summary { get; set; }
	public List<string> Categories { get; set; } = [];
}