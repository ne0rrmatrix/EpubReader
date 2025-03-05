using EpubReader.Models;

namespace EpubReader.ViewModels;

public partial class SettingsPageViewModel : BaseViewModel
{
	readonly List<EbookFonts> fonts = [
	new EbookFonts { FontFamily = "Arial" },
		new EbookFonts { FontFamily = "Times New Roman" },
		new EbookFonts { FontFamily = "Verdana" },
		new EbookFonts { FontFamily = "Courier New" },
		new EbookFonts { FontFamily = "Georgia" },
		new EbookFonts { FontFamily = "Tahoma" },
		new EbookFonts { FontFamily = "Trebuchet MS" },
		new EbookFonts { FontFamily = "Comic Sans MS" },
		new EbookFonts { FontFamily = "Lucida Sans Unicode" },
		new EbookFonts { FontFamily = "Helvetica" }
];
	readonly List<ColorScheme> colorSchemes =
		[
			new ColorScheme() { Name = "Light", BackgroundColor = "#FFFFFF" , TextColor = "#000000"},
			new ColorScheme() { Name = "Dark", BackgroundColor = "#121212", TextColor = "#E1E1E1" },
			new ColorScheme() { Name = "Sepia", BackgroundColor = "#f4ecd8", TextColor = "#5b4636" },
			new ColorScheme() { Name = "Forest", BackgroundColor = "#e0f2e9", TextColor = "#2e4d38" },
			new ColorScheme() { Name = "Ocean", BackgroundColor = "#e0f7fa", TextColor = "#01579b" },
			new ColorScheme() { Name = "Sand", BackgroundColor = "#f5deb3", TextColor = "#000000" },
			new ColorScheme() { Name = "Charcoal", BackgroundColor = "#36454f", TextColor = "#dcdcdc" },
			new ColorScheme() { Name = "Vintage", BackgroundColor = "#f5f5dc", TextColor = "#000000" }
		];
	public List<ColorScheme> ColorSchemes => colorSchemes;
	public List<EbookFonts> Fonts => fonts;
	public SettingsPageViewModel()
	{
	}
}
