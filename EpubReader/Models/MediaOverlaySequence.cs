namespace EpubReader.Models.MediaOverlays;

public sealed class MediaOverlaySequence : MediaOverlayNode
{
	public string? TextReference { get; init; }

	public List<MediaOverlayNode> Children { get; } = [];
}