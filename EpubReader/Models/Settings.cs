using SQLite;

namespace EpubReader.Models;

[Table("settings")]
public class Settings
{
	[PrimaryKey, AutoIncrement]
	[Column("Id")]
	public Guid Id { get; set; }
	[Column("FontFamily")]
	public string FontFamily { get; set; } = string.Empty;
	[Column("FontSize")]
	public int FontSize { get; set; } = 10;
	[Column("BackgroundColor")]
	public string BackgroundColor { get; set; } = string.Empty;
	[Column("TextColor")]
	public string TextColor { get; set; } = string.Empty;
	[Column("ColorScheme")]
	public string ColorScheme { get; set; } = string.Empty;
}
