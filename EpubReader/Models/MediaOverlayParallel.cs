namespace EpubReader.Models.MediaOverlays;

public sealed class MediaOverlayParallel : MediaOverlayNode
{
	public MediaOverlayText? Text { get; init; }

	public MediaOverlayAudio? Audio { get; init; }
}
