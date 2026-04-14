namespace EpubReader.Models.MediaOverlays;

public sealed class MediaOverlayParseResult(IReadOnlyList<MediaOverlayDocument> documents, string? activeClass, string? playbackActiveClass, string? narrator, TimeSpan? duration)
{
	public static MediaOverlayParseResult Empty { get; } = new([], null, null, null, null);

	public IReadOnlyList<MediaOverlayDocument> Documents { get; } = documents;

	public string? ActiveClass { get; } = activeClass;

	public string? PlaybackActiveClass { get; } = playbackActiveClass;

	public string? Narrator { get; } = narrator;

	public TimeSpan? Duration { get; } = duration;
}