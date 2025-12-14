namespace EpubReader.Models;

/// <summary>
/// Represents syncable Media Overlay playback state for a narrated EPUB section.
/// </summary>
public sealed record MediaOverlayPlaybackProgress(
	bool Enabled,
	int ChapterIndex,
	int SegmentIndex,
	double? PositionSeconds,
	string? FragmentId
);