namespace EpubReader.Service;

static class EbookColors
{
	const string defaultBlack = "#000000";
	const string defaultWhite = "#ffffff";
	public const string DarkBackgroundColor = "#121212";
	public const string DarkTextColor = "#E1E1E1";
	public const string DarkNavigationBarColor = "#121212";
	public const string DefaultBackgroundColor = defaultWhite;
	public const string DefaultTextColor = defaultBlack;
	public const string DefaultNavigationBarColor = defaultWhite;
	public const string SepiaBackgroundColor = "#f4ecd8";
	public const string SepiaTextColor = "#5b4636";
	public const string SepiaNavigationBarColor = "#E1E1E1";
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
public enum EbookColor
{
	Dark,
	Sepia,
	Default,
	Forest,
	Ocean,
	Sand,
	Charcoal,
	Vintage
}
public class EbookColorScheme
{
	public string BackgroundColor { get; set; } = string.Empty;
	public string TextColor { get; set; } = string.Empty;
	public string NavigationBarColor { get; set; } = string.Empty;

	public static (string BackgroundColor, string TextColor, string NavigationBarColor) GetColorSchemeString(EbookColor color)
	{
		return color switch
		{
			EbookColor.Dark => (EbookColors.DarkBackgroundColor, EbookColors.DarkTextColor, EbookColors.DarkNavigationBarColor),
			EbookColor.Sepia => (EbookColors.SepiaBackgroundColor, EbookColors.SepiaTextColor, EbookColors.SepiaNavigationBarColor),
			EbookColor.Default => (EbookColors.DefaultBackgroundColor, EbookColors.DefaultTextColor, EbookColors.DefaultNavigationBarColor),
			EbookColor.Forest => (EbookColors.Forest, EbookColors.ForestTextColor, EbookColors.ForestNavigationBarColor),
			EbookColor.Ocean => (EbookColors.Ocean, EbookColors.OceanTextColor, EbookColors.OceanNavigationBarColor),
			EbookColor.Sand => (EbookColors.Sand, EbookColors.SandTextColor, EbookColors.SandNavigationBarColor),
			EbookColor.Charcoal => (EbookColors.Charcoal, EbookColors.CharcoalTextColor, EbookColors.CharcoalNavigationBarColor),
			EbookColor.Vintage => (EbookColors.Vintage, EbookColors.VintageTextColor, EbookColors.VintageNavigationBarColor),
			_ => (string.Empty, string.Empty, string.Empty)
		};
	}
	public static (Color? BackgroundColor, Color? TextColor, Color? NavigationBarColor) GetColorSchemeColor(EbookColor color)
	{
		return color switch
		{
			EbookColor.Dark => (Color.FromRgba(EbookColors.DarkBackgroundColor), Color.FromRgba(EbookColors.DarkTextColor), Color.FromRgba(EbookColors.DarkNavigationBarColor)),
			EbookColor.Sepia => (Color.FromRgba(EbookColors.SepiaBackgroundColor), Color.FromRgba(EbookColors.SepiaTextColor), Color.FromRgba(EbookColors.SepiaNavigationBarColor)),
			EbookColor.Default => (Color.FromRgba(EbookColors.DefaultBackgroundColor), Color.FromRgba(EbookColors.DefaultTextColor), Color.FromRgba(EbookColors.DefaultNavigationBarColor)),
			EbookColor.Forest => (Color.FromRgba(EbookColors.Forest), Color.FromRgba(EbookColors.ForestTextColor), Color.FromRgba(EbookColors.ForestNavigationBarColor)),
			EbookColor.Ocean => (Color.FromRgba(EbookColors.Ocean), Color.FromRgba(EbookColors.OceanTextColor), Color.FromRgba(EbookColors.OceanNavigationBarColor)),
			EbookColor.Sand => (Color.FromRgba(EbookColors.Sand), Color.FromRgba(EbookColors.SandTextColor), Color.FromRgba(EbookColors.SandNavigationBarColor)),
			EbookColor.Charcoal => (Color.FromRgba(EbookColors.Charcoal), Color.FromRgba(EbookColors.CharcoalTextColor), Color.FromRgba(EbookColors.CharcoalNavigationBarColor)),
			EbookColor.Vintage => (Color.FromRgba(EbookColors.Vintage), Color.FromRgba(EbookColors.VintageTextColor), Color.FromRgba(EbookColors.VintageNavigationBarColor)),
			_ => (null, null, null)
		};
	}
}