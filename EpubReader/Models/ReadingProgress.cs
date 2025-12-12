using SQLite;

namespace EpubReader.Models;

/// <summary>
/// Represents per-book reading progress used for local persistence and cloud sync.
/// </summary>
[Table("ReadingProgress")]
public class ReadingProgress
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

	public override string ToString() => $"{BookId}: chapter {CurrentChapter}, page {CurrentPage} at {LastUpdated} on {DeviceId}";
}