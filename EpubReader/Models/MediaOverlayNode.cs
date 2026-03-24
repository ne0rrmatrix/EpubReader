namespace EpubReader.Models.MediaOverlays;

/// <summary>
/// Base node for SMIL sequences and parallels.
/// </summary>
public abstract class MediaOverlayNode
{
	public string? Id { get; init; }

	public string? EpubType { get; init; }
}
