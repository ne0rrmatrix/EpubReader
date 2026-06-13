namespace EpubReader.MediaOverlay;

public sealed class MediaOverlayParallel : MediaOverlayNode
{
	public MediaOverlayText? Text { get; init; }

	public MediaOverlayAudio? Audio { get; init; }
}