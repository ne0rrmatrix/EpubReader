using System.ComponentModel.DataAnnotations.Schema;
namespace EpubReader.Models;

/// <summary>
/// Represents a pending cloud sync operation stored locally for retry.
/// </summary>
public class SyncQueueItem : Shared
{
	public int Id { get; set; }

	public string BookId { get; set; } = string.Empty;

	public int CurrentPage { get; set; }

	public int CurrentChapter { get; set; }

	public int CharacterPosition { get; set; }

	/// <summary>
	/// ISO 8601 UTC timestamp of when the sync was queued.
	/// </summary>
	public string Timestamp { get; set; } = DateTimeOffset.UtcNow.ToString("o");

	public int RetryCount { get; set; }

	// --- Media Overlay playback sync ---
	public bool? MediaOverlayEnabled { get; set; }

	public int? MediaOverlayChapter { get; set; }

	public int? MediaOverlaySegmentIndex { get; set; }

	public double? MediaOverlayPositionSeconds { get; set; }

	public string? MediaOverlayFragmentId { get; set; }
}