using EpubReader.Models;

namespace EpubReader.ViewModels;

/// <summary>
/// Represents the view model for the settings page, providing access to available fonts and color schemes.
/// </summary>
/// <remarks>This class is responsible for managing the collections of fonts and color schemes that can be used in
/// an EPUB document. It provides properties to access these collections, which are initialized with a predefined set of
/// options.</remarks>
public partial class SettingsPageViewModel : BaseViewModel
{
	readonly List<EpubFonts> fonts = [
		new EpubFonts { FontFamily = "Arial" },
		new EpubFonts { FontFamily = "Times New Roman" },
		new EpubFonts { FontFamily = "Verdana" },
		new EpubFonts { FontFamily = "Courier New" },
		new EpubFonts { FontFamily = "Georgia" },
		new EpubFonts { FontFamily = "Tahoma" },
		new EpubFonts { FontFamily = "Trebuchet MS" },
		new EpubFonts { FontFamily = "Comic Sans MS" },
		new EpubFonts { FontFamily = "Helvetica" }
];
	readonly List<ColorScheme> colorSchemes =
		[
			new ColorScheme() { Name = "Light", BackgroundColor = "#FFFFFF" , TextColor = "#000000"},
			new ColorScheme() { Name = "Dark", BackgroundColor = "#121212", TextColor = "#E1E1E1" },
			new ColorScheme() { Name = "Sepia", BackgroundColor = "#f4ecd8", TextColor = "#5b4636" },
			new ColorScheme() { Name = "Ocean", BackgroundColor = "#e0f7fa", TextColor = "#01579b" },
			new ColorScheme() { Name = "Sand", BackgroundColor = "#f5deb3", TextColor = "#000000" },
			new ColorScheme() { Name = "Charcoal", BackgroundColor = "#36454f", TextColor = "#dcdcdc" },
			new ColorScheme() { Name = "Vintage", BackgroundColor = "#f5f5dc", TextColor = "#000000" }
		];

	/// <summary>
	/// Gets the collection of available color schemes.
	/// </summary>
	public List<ColorScheme> ColorSchemes => colorSchemes;

	/// <summary>
	/// Gets the collection of fonts used in the EPUB document.
	/// </summary>
	public List<EpubFonts> Fonts => fonts;

	/// <summary>
	/// Initializes a new instance of the <see cref="SettingsPageViewModel"/> class.
	/// </summary>
	public SettingsPageViewModel()
	{
	}
}
