namespace EpubReader.Service;

static class CustomColors
{
	const string defaultBlack = "#000000";
	const string defaultWhite = "#ffffff";
	public const string DarkBackgroundColor = "#121212";
	public const string DarkTextColor = "#E1E1E1";
	public const string DarkNavigationBarColor = "#121212";
	public const string SepiaBackgroundColor = "#f4ecd8";
	public const string SepiaTextColor = "#5b4636";
	public const string SepiaNavigationBarColor = "#E1E1E1";
	public const string NightModeBackgroundColor = defaultBlack;
	public const string NightModeTextColor = defaultWhite;
	public const string NightModeNavigationBarColor = defaultBlack;
	public const string DaylightBackgroundColor = defaultWhite;
	public const string DaylightTextColor = defaultBlack;
	public const string DaylightNavigationBarColor = defaultWhite;
	public const string DefaultBackgroundColor = defaultWhite;
	public const string DefaultTextColor = defaultBlack;
	public const string DefaultNavigationBarColor = defaultWhite;
	public const string Forest = "#e0f2e9";
	public const string ForestTextColor = "#2e4d38";
	public const string ForestNavigationBarColor = "#e0f2e9";
	public const string Ocean = "#e0f7fa";
	public const string OceanTextColor = "#01579b";
	public const string OceanNavigationBarColor = "#e0f7fa";
	public const string Sand = "#f5deb3";
	public const string SandTextColor = defaultBlack;
	public const string SandNavigationBarColor = "#f5deb3";
	public const string Charcoal = "#36454f";
	public const string CharcoalTextColor = "#dcdcdc";
	public const string CharcoalNavigationBarColor = "#36454f";
	public const string Vintage = "#f5f5dc";
	public const string VintageTextColor = defaultBlack;
	public const string VintageNavigationBarColor = "#f5f5dc";
}
public enum CustomColor
{
	Dark,
	Sepia,
	NightMode,
	Daylight,
	Default,
	Forest,
	Ocean,
	Sand,
	Charcoal,
	Vintage
}
public class CustomColorScheme
{
	public string BackgroundColor { get; set; } = string.Empty;
	public string TextColor { get; set; } = string.Empty;
	public string NavigationBarColor { get; set; } = string.Empty;

	public static (string BackgroundColor, string TextColor, string NavigationBarColor) GetColorSchemeString(CustomColor color)
	{
		return color switch
		{
			CustomColor.Dark => (CustomColors.DarkBackgroundColor, CustomColors.DarkTextColor, CustomColors.DarkNavigationBarColor),
			CustomColor.Sepia => (CustomColors.SepiaBackgroundColor, CustomColors.SepiaTextColor, CustomColors.SepiaNavigationBarColor),
			CustomColor.NightMode => (CustomColors.NightModeBackgroundColor, CustomColors.NightModeTextColor, CustomColors.NightModeNavigationBarColor),
			CustomColor.Daylight => (CustomColors.DaylightBackgroundColor, CustomColors.DaylightTextColor, CustomColors.DaylightNavigationBarColor),
			CustomColor.Default => (CustomColors.DefaultBackgroundColor, CustomColors.DefaultTextColor, CustomColors.DefaultNavigationBarColor),
			CustomColor.Forest => (CustomColors.Forest, CustomColors.ForestTextColor, CustomColors.ForestNavigationBarColor),
			CustomColor.Ocean => (CustomColors.Ocean, CustomColors.OceanTextColor, CustomColors.OceanNavigationBarColor),
			CustomColor.Sand => (CustomColors.Sand, CustomColors.SandTextColor, CustomColors.SandNavigationBarColor),
			CustomColor.Charcoal => (CustomColors.Charcoal, CustomColors.CharcoalTextColor, CustomColors.CharcoalNavigationBarColor),
			CustomColor.Vintage => (CustomColors.Vintage, CustomColors.VintageTextColor, CustomColors.VintageNavigationBarColor),
			_ => (string.Empty, string.Empty, string.Empty)
		};
	}
	public static (Color? BackgroundColor, Color? TextColor, Color? NavigationBarColor) GetColorSchemeColor(CustomColor color)
	{
		return color switch
		{
			CustomColor.Dark => (Color.FromRgba(CustomColors.DarkBackgroundColor), Color.FromRgba(CustomColors.DarkTextColor), Color.FromRgba(CustomColors.DarkNavigationBarColor)),
			CustomColor.Sepia => (Color.FromRgba(CustomColors.SepiaBackgroundColor), Color.FromRgba(CustomColors.SepiaTextColor), Color.FromRgba(CustomColors.SepiaNavigationBarColor)),
			CustomColor.NightMode => (Color.FromRgba(CustomColors.NightModeBackgroundColor), Color.FromRgba(CustomColors.NightModeTextColor), Color.FromRgba(CustomColors.NightModeNavigationBarColor)),
			CustomColor.Daylight => (Color.FromRgba(CustomColors.DaylightBackgroundColor), Color.FromRgba(CustomColors.DaylightTextColor), Color.FromRgba(CustomColors.DaylightNavigationBarColor)),
			CustomColor.Default => (Color.FromRgba(CustomColors.DefaultBackgroundColor), Color.FromRgba(CustomColors.DefaultTextColor), Color.FromRgba(CustomColors.DefaultNavigationBarColor)),
			CustomColor.Forest => (Color.FromRgba(CustomColors.Forest), Color.FromRgba(CustomColors.ForestTextColor), Color.FromRgba(CustomColors.ForestNavigationBarColor)),
			CustomColor.Ocean => (Color.FromRgba(CustomColors.Ocean), Color.FromRgba(CustomColors.OceanTextColor), Color.FromRgba(CustomColors.OceanNavigationBarColor)),
			CustomColor.Sand => (Color.FromRgba(CustomColors.Sand), Color.FromRgba(CustomColors.SandTextColor), Color.FromRgba(CustomColors.SandNavigationBarColor)),
			CustomColor.Charcoal => (Color.FromRgba(CustomColors.Charcoal), Color.FromRgba(CustomColors.CharcoalTextColor), Color.FromRgba(CustomColors.CharcoalNavigationBarColor)),
			CustomColor.Vintage => (Color.FromRgba(CustomColors.Vintage), Color.FromRgba(CustomColors.VintageTextColor), Color.FromRgba(CustomColors.VintageNavigationBarColor)),
			_ => (null, null, null)
		};
	}
}