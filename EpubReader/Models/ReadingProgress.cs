using SQLite;

namespace EpubReader.Models;

/// <summary>
/// Represents per-book reading progress used for local persistence and cloud sync.
/// </summary>
[Table("ReadingProgress")]
public class ReadingProgress : Shared
{
	[PrimaryKey]
	[Column("BookId")]
	public string BookId { get; set; } = string.Empty;

	[Column("CurrentPage")]
	public int CurrentPage { get; set; }

	[Column("CurrentChapter")]
	public int CurrentChapter { get; set; }

	/// <summary>
	/// ISO 8601 UTC timestamp of the last update.
	/// </summary>
	[Column("LastUpdated")]
	public string LastUpdated { get; set; } = DateTimeOffset.UtcNow.ToString("o");

	[Column("DeviceId")]
	public string DeviceId { get; set; } = string.Empty;

	[Column("DeviceName")]
	public string DeviceName { get; set; } = string.Empty;

	[Column("IsSynced")]
	public bool IsSynced { get; set; }

	// --- Media Overlay playback sync ---
	// These fields capture the user's current narrated playback position within the current chapter.
	// They are optional because many books do not ship media overlays.

	[Column("MediaOverlayEnabled")]
	public bool? MediaOverlayEnabled { get; set; }

	[Column("MediaOverlayChapter")]
	public int? MediaOverlayChapter { get; set; }

	// Zero-based segment index within the chapter's flattened SMIL parallels.
	[Column("MediaOverlaySegmentIndex")]
	public int? MediaOverlaySegmentIndex { get; set; }

	// Absolute position within the chapter in seconds (0..duration).
	[Column("MediaOverlayPositionSeconds")]
	public double? MediaOverlayPositionSeconds { get; set; }

	// EPUB fragment id (e.g., element id) used for highlight restoration.
	[Column("MediaOverlayFragmentId")]
	public string? MediaOverlayFragmentId { get; set; }

	public override string ToString()
		=> $"{BookId}: chapter {CurrentChapter}, page {CurrentPage} at {LastUpdated} on {DeviceId} (MO: {MediaOverlayChapter}/{MediaOverlaySegmentIndex}@{MediaOverlayPositionSeconds})";
}