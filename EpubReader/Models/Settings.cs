using EpubReader.Service;
using SQLite;

namespace EpubReader.Models;

[Table("settings")]
public class Settings
{
	[PrimaryKey, AutoIncrement, Column("Id")]
	public int Id { get; set; }
	public string FontFamily { get; set; } = "Times New Roman";
	public int FontSize { get; set; } = 10;
	public string BackgroundColor { get; set; } = string.Empty;
	public string SetBackgroundColor { get; set; } = string.Empty;
	public string SetTextColor { get; set; } = string.Empty;
	public string TextColor { get; set; } = string.Empty;
	public string ColorScheme { get; set; } = "Light";
}
