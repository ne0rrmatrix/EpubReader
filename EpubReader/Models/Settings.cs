using SQLite;

namespace EpubReader.Models;

/// <summary>
/// Represents the application settings that can be configured by the user.
/// </summary>
/// <remarks>This class is used to store various display and layout preferences such as font settings, color
/// schemes, and layout options. It is mapped to a database table named "settings".</remarks>
[Table("settings")]
public class Settings
{
	/// <summary>
	/// Gets or sets the unique identifier for the entity.
	/// </summary>
	[PrimaryKey, AutoIncrement]
	[Column("Id")]
	public Guid Id { get; set; }

	/// <summary>
	/// Gets or sets the font family name used for text rendering.
	/// </summary>
	[Column("FontFamily")]
	public string FontFamily { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the font size for the text.
	/// </summary>
	[Column("FontSize")]
	public int FontSize { get; set; } = 0;

	/// <summary>
	/// Gets or sets the background color as a string representation.
	/// </summary>
	[Column("BackgroundColor")]
	public string BackgroundColor { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the text color as a string representation.
	/// </summary>
	[Column("TextColor")]
	public string TextColor { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the color scheme name for the application.
	/// </summary>
	[Column("ColorScheme")]
	public string ColorScheme { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether multiple columns are supported.
	/// </summary>
	[Column("SupportMultipleColumns")]
	public bool SupportMultipleColumns { get; set; } = false;
}
