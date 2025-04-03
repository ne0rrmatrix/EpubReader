using EpubReader.Models;

namespace EpubReader.ViewModels;

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
	public List<ColorScheme> ColorSchemes => colorSchemes;
	public List<EpubFonts> Fonts => fonts;
	public SettingsPageViewModel()
	{
	}
}
