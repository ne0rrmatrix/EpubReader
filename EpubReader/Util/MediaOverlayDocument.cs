using EpubReader.Models.MediaOverlays;

namespace EpubReader.Util;

/// <summary>
/// Represents a parsed SMIL media overlay document.
/// </summary>
public sealed class MediaOverlayDocument(string id, string href, MediaOverlaySequence body, IReadOnlyList<MediaOverlayParallel> flattenedNodes)
{
	public string Id { get; } = id;

	public string Href { get; } = href;

	public MediaOverlaySequence Body { get; } = body;

	public IReadOnlyList<MediaOverlayParallel> FlattenedNodes { get; } = flattenedNodes;

	public List<string> AssociatedContentDocuments { get; } = [];

	public void AddAssociatedDocument(string href)
	{
		if (string.IsNullOrEmpty(href) || AssociatedContentDocuments.Contains(href, StringComparer.OrdinalIgnoreCase))
		{
			return;
		}

		AssociatedContentDocuments.Add(href);
	}
}