namespace EpubReader.Models;

/// <summary>
/// Represents the application settings that can be configured by the user.
/// </summary>
/// <remarks>This class is used to store various display and layout preferences such as font settings, color
/// schemes, and layout options. It is mapped to a database table named "settings".</remarks>
public class Settings
{
	/// <summary>
	/// Gets or sets the unique identifier for the entity.
	/// </summary>
	public Guid Id { get; set; }

	/// <summary>
	/// Gets or sets the font family name used for text rendering.
	/// </summary>
	public string FontFamily { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the font size for the text.
	/// </summary>
	public int FontSize { get; set; } = 16;

	/// <summary>
	/// Gets or sets the preferred line spacing for reader content.
	/// </summary>
	public string LineSpacing { get; set; } = "1.5";

	/// <summary>
	/// Gets or sets the preferred text alignment for reader content.
	/// </summary>
	public string TextAlignment { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the preferred paragraph spacing for reader content.
	/// </summary>
	public string ParagraphSpacing { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the preferred hyphenation mode for reader content.
	/// </summary>
	public string BodyHyphens { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the preferred letter spacing for reader content.
	/// </summary>
	public string LetterSpacing { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the preferred word spacing for reader content.
	/// </summary>
	public string WordSpacing { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the background color as a string representation.
	/// </summary>
	public string BackgroundColor { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the text color as a string representation.
	/// </summary>
	public string TextColor { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the color scheme name for the application.
	/// </summary>
	public string ColorScheme { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether multiple columns are supported.
	/// </summary>
	public bool SupportMultipleColumns { get; set; } = false;

	/// <summary>
	/// Gets or sets a value indicating whether Calibre Server auto-discovery is enabled.
	/// </summary>
	public bool CalibreAutoDiscovery { get; set; } = true;

	/// <summary>
	/// Gets or sets the port number used by the server.
	/// </summary>
	public int Port { get; set; } = 8080; // Default Calibre server port

	/// <summary>
	/// Gets or sets the IP address of the Calibre server.
	/// </summary>
	public string IPAddress { get; set; } = "localhost"; // Default Calibre server IP address

	/// <summary>
	/// Gets or sets the URL prefix used for the Calibre server.
	/// </summary>
	public string UrlPrefix { get; set; } = "http"; // Default URL prefix for Calibre server

	/// <summary>
	/// Gets or sets the user-specified Calibre server port used when auto discovery is disabled.
	/// </summary>
	public int CalibreManualPort { get; set; } = 8080;

	/// <summary>
	/// Gets or sets the user-specified Calibre server host or IP address used when auto discovery is disabled.
	/// </summary>
	public string CalibreManualIPAddress { get; set; } = "localhost";

	/// <summary>
	/// Gets or sets the user-specified URL prefix used when auto discovery is disabled.
	/// </summary>
	public string CalibreManualUrlPrefix { get; set; } = "http";
}