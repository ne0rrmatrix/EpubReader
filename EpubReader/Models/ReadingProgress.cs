namespace EpubReader.Models;

/// <summary>
/// Represents per-book reading progress used for local persistence and cloud sync.
/// </summary>
public class ReadingProgress : Shared
{
	public string BookId { get; set; } = string.Empty;

	public int CurrentPage { get; set; }

	public int CurrentChapter { get; set; }

	/// <summary>
	/// Device-independent character position within the current chapter, used for
	/// cross-device position restoration. 0 means no character position is available
	/// (backwards-compatible fallback to CurrentPage).
	/// </summary>
	public int CharacterPosition { get; set; }

	/// <summary>
	/// ISO 8601 UTC timestamp of the last update.
	/// </summary>
	public string LastUpdated { get; set; } = DateTimeOffset.UtcNow.ToString("o");

	public string DeviceId { get; set; } = string.Empty;

	public string DeviceName { get; set; } = string.Empty;

	public bool IsSynced { get; set; }

	// --- Media Overlay playback sync ---
	// These fields capture the user's current narrated playback position within the current chapter.
	// They are optional because many books do not ship media overlays.

	public bool? MediaOverlayEnabled { get; set; }

	public int? MediaOverlayChapter { get; set; }

	// Zero-based segment index within the chapter's flattened SMIL parallels.
	public int? MediaOverlaySegmentIndex { get; set; }

	// Absolute position within the chapter in seconds (0..duration).
	public double? MediaOverlayPositionSeconds { get; set; }

	// EPUB fragment id (e.g., element id) used for highlight restoration.
	public string? MediaOverlayFragmentId { get; set; }

	public override string ToString()
		=> $"{BookId}: chapter {CurrentChapter}, page {CurrentPage}, charPos {CharacterPosition} at {LastUpdated} on {DeviceId} (MO: {MediaOverlayChapter}/{MediaOverlaySegmentIndex}@{MediaOverlayPositionSeconds})";

	/// <summary>
	/// Builds a minimal <see cref="ReadingProgress"/> snapshot from a known chapter/page
	/// position, for backfilling a progress record for a book that predates progress tracking.
	/// </summary>
	public static ReadingProgress FromBookPosition(string bookId, int currentChapter, int currentPage, int characterPosition = 0, string? lastUpdated = null)
	{
		return new ReadingProgress
		{
			BookId = bookId,
			CurrentChapter = currentChapter,
			CurrentPage = currentPage,
			CharacterPosition = characterPosition,
			LastUpdated = string.IsNullOrWhiteSpace(lastUpdated) ? DateTimeOffset.UtcNow.ToString("o") : lastUpdated,
			DeviceId = string.Empty,
			DeviceName = string.Empty,
			IsSynced = false
		};
	}
}