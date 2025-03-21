using System.Text.RegularExpressions;

namespace EpubReader.Util;
public static partial class FontDeclarationValidator
{
	public static bool ContainsOpenSourceFontAlternatives(string cssContent)
	{
		if (string.IsNullOrWhiteSpace(cssContent))
		{
			return false;
		}

		// Check for Nimbus Roman as alternative for Times and Times New Roman
		bool hasNimbusRomanAlternative = CheckNimbusRomanAlternative(cssContent);

		// Check for Nimbus Sans as alternative for Helvetica and Arial
		bool hasNimbusSansAlternative = CheckNimbusSansAlternative(cssContent);

		// Return true only if both alternatives are properly declared
		return hasNimbusRomanAlternative && hasNimbusSansAlternative;
	}

	static bool CheckNimbusRomanAlternative(string cssContent)
	{
		// Look for font-family declarations containing Times or Times New Roman with Nimbus Roman
		var timesNewRomanPattern = TimesNewRoman();
		var timesPattern = Times();

		// Reversed case - Nimbus Roman comes first
		var nimbusRomanTimesNewRomanPattern = NimbusRomanTimesNewRoman();
		var nimbusRomanTimesPattern = NimbusRomanTimes();

		return timesNewRomanPattern.IsMatch(cssContent) ||
			   timesPattern.IsMatch(cssContent) ||
			   nimbusRomanTimesNewRomanPattern.IsMatch(cssContent) ||
			   nimbusRomanTimesPattern.IsMatch(cssContent);
	}

	static bool CheckNimbusSansAlternative(string cssContent)
	{
		// Look for font-family declarations containing Helvetica or Arial with Nimbus Sans
		var helveticaPattern = MyRegex();
		var arialPattern = Arial();

		// Reversed case - Nimbus Sans comes first
		var nimbusSansHelveticaPattern = NimbusSansHelvetica();
		var nimbusSansArialPattern = NimbusSansArial();

		return helveticaPattern.IsMatch(cssContent) ||
			   arialPattern.IsMatch(cssContent) ||
			   nimbusSansHelveticaPattern.IsMatch(cssContent) ||
			   nimbusSansArialPattern.IsMatch(cssContent);
	}

	[GeneratedRegex(@"font-family\s*:\s*(?:['""]Times New Roman['""]|Times New Roman).*?(?:['""]Nimbus Roman['""]|Nimbus Roman)", RegexOptions.IgnoreCase, "en-US")]
	private static partial Regex TimesNewRoman();
	[GeneratedRegex(@"font-family\s*:\s*(?:['""]Nimbus Sans['""]|Nimbus Sans).*?(?:['""]Helvetica['""]|Helvetica)", RegexOptions.IgnoreCase, "en-US")]
	private static partial Regex NimbusSansHelvetica();
	[GeneratedRegex(@"font-family\s*:\s*(?:['""]Nimbus Sans['""]|Nimbus Sans).*?(?:['""]Arial['""]|Arial)", RegexOptions.IgnoreCase, "en-US")]
	private static partial Regex NimbusSansArial();
	[GeneratedRegex(@"font-family\s*:\s*(?:['""]Arial['""]|Arial).*?(?:['""]Nimbus Sans['""]|Nimbus Sans)", RegexOptions.IgnoreCase, "en-US")]
	private static partial Regex Arial();
	[GeneratedRegex(@"font-family\s*:\s*(?:['""]Helvetica['""]|Helvetica).*?(?:['""]Nimbus Sans['""]|Nimbus Sans)", RegexOptions.IgnoreCase, "en-US")]
	private static partial Regex MyRegex();
	[GeneratedRegex(@"font-family\s*:\s*(?:['""]Times['""]|Times).*?(?:['""]Nimbus Roman['""]|Nimbus Roman)", RegexOptions.IgnoreCase, "en-US")]
	private static partial Regex Times();
	[GeneratedRegex(@"font-family\s*:\s*(?:['""]Nimbus Roman['""]|Nimbus Roman).*?(?:['""]Times New Roman['""]|Times New Roman)", RegexOptions.IgnoreCase, "en-US")]
	private static partial Regex NimbusRomanTimesNewRoman();
	[GeneratedRegex(@"font-family\s*:\s*(?:['""]Nimbus Roman['""]|Nimbus Roman).*?(?:['""]Times['""]|Times)", RegexOptions.IgnoreCase, "en-US")]
	private static partial Regex NimbusRomanTimes();
}
