namespace EpubReader.Models;

/// <summary>
/// Represents a link in an OPDS feed or entry.
/// </summary>
public class OpdsLink
{
	public string? Href { get; set; }
	public string? Type { get; set; }
	public string? Rel { get; set; }
	public string? Title { get; set; }
}
