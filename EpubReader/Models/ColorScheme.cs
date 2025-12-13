namespace EpubReader.Models;

/// <summary>
/// Represents a reading-friendly color scheme used for EPUB rendering and app theming.
/// </summary>
/// <remarks>
/// Each scheme exposes a display `Name`, a `BackgroundColor` and a `TextColor` (foreground).
/// Colors are stored as hex strings (e.g. "#FFFFFF"). Keep property names stable to avoid
/// breaking existing bindings and persisted settings.
/// </remarks>
public class ColorScheme
{
	/// <summary>
	/// Display name for the color scheme (e.g. "Material Light", "Sepia").
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Background color (hex string). Use subtle, low‑glare backgrounds for long reading sessions.
	/// </summary>
	public string BackgroundColor { get; set; } = "#FAFAFA";

	/// <summary>
	/// Main text (foreground) color (hex string).
	/// </summary>
	public string TextColor { get; set; } = "#111827";
}