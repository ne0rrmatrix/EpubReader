namespace EpubReader.Models.MediaOverlays;

public sealed class MediaOverlayAudioResource
{
	public string RelativePath { get; init; } = string.Empty;

	public string NormalizedPath { get; init; } = string.Empty;

	public byte[] Content { get; init; } = [];

	public string? ContentType { get; init; }
}
