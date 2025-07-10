namespace EpubReader.Models;

/// <summary>
/// Represents a color scheme with specific background and text colors.
/// </summary>
/// <remarks>This class is used to define a set of colors for UI elements, allowing for consistent styling across
/// an application. Each color scheme has a name, a background color, and a text color.</remarks>
public class ColorScheme
{
	/// <summary>
	/// Gets or sets the background color as a string.
	/// </summary>
	public string BackgroundColor { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the text color as a string representation.
	/// </summary>
	public string TextColor { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the name associated with the <code>ColorScheme</code>.
	/// </summary>
	public string Name { get; set; } = string.Empty;
}
