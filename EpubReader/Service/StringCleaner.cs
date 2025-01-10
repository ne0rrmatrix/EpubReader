using System.Text.RegularExpressions;

namespace EpubReader.Service;

public partial class StringCleaner
{
	static readonly Regex pagePattern = MyRegex();

	public static string GetPageNumberInfo(string input)
	{
		return pagePattern.Replace(input, "");
	}

	[GeneratedRegex(@"#page_\d+$")]
	private static partial Regex MyRegex();
}
