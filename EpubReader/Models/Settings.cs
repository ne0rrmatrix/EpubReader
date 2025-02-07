using SQLite;

namespace EpubReader.Models;

[Table("settings")]
public class Settings
{
	[PrimaryKey, AutoIncrement, Column("Id")]
	public int Id { get; set; }
	public bool IsSystemMode { get; set; } = false;
	public string FontFamily { get; set; } = string.Empty;
	public int FontSize { get; set; }
	public string BackgroundColor { get; set; } = string.Empty;
	public string TextColor { get; set; } = string.Empty;
}
