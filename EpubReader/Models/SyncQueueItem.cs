using SQLite;

namespace EpubReader.Models;

/// <summary>
/// Represents a pending cloud sync operation stored locally for retry.
/// </summary>
[Table("SyncQueue")]
public class SyncQueueItem
{
	[PrimaryKey, AutoIncrement]
	[Column("Id")]
	public int Id { get; set; }

	[Column("BookId")]
	public string BookId { get; set; } = string.Empty;

	[Column("CurrentPage")]
	public int CurrentPage { get; set; }

	[Column("CurrentChapter")]
	public int CurrentChapter { get; set; }

	/// <summary>
	/// ISO 8601 UTC timestamp of when the sync was queued.
	/// </summary>
	[Column("Timestamp")]
	public string Timestamp { get; set; } = DateTimeOffset.UtcNow.ToString("o");

	[Column("RetryCount")]
	public int RetryCount { get; set; }
}