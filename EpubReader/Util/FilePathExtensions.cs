using System.Text.RegularExpressions;

namespace EpubReader.Util;

public static class FilePathExtensions
{
	public static string UpdateImagePathsToFilenames(string htmlString)
	{
		// Regular expression to find <img> tags and capture the src attribute value
		string pattern = @"<img\s+[^>]*?src=(['""])(.*?)\1[^>]*?>";

		return Regex.Replace(htmlString, pattern, match =>
		{
			string originalPath = match.Groups[2].Value;

			if (!string.IsNullOrEmpty(originalPath))
			{
				string filename = Path.GetFileName(originalPath);
				return match.Value.Replace(originalPath, filename);
			}

			return match.Value;
		}, RegexOptions.IgnoreCase, matchTimeout: TimeSpan.FromSeconds(10));
	}

	public static string UpdateSvgLinks(string html)
	{
		// Regular expression to find SVG links in the HTML content
		string pattern = @"<img[^>]+src=""([^""]+\.svg)""";

		// Use Regex to find matches and replace the src with the filename
		html = Regex.Replace(html, pattern, match =>
		{
			string fullPath = match.Groups[1].Value;
			string fileName = Path.GetFileName(fullPath);
			return match.Value.Replace(fullPath, fileName);
		}, RegexOptions.IgnoreCase, matchTimeout: TimeSpan.FromSeconds(10));

		// Pattern to match the 'data' attribute in <object> tags and 'xlink:href' in <image> tags
		string pattern1 = @"(data|xlink:href)=""([^""]*\.svg|[^""]*\.png)""";

		// Use Regex.Replace to find and replace the paths
		html = Regex.Replace(html, pattern1, match =>
		{
			string attributeName = match.Groups[1].Value;
			string filePath = match.Groups[2].Value;
			string fileName = Path.GetFileName(filePath);
			return $"{attributeName}=\"{fileName}\"";
		}, RegexOptions.IgnoreCase, matchTimeout: TimeSpan.FromSeconds(10));
		return html;
	}

	public static string SetFontFilenames(string cssContent)
	{
		string pattern = @"url\((['""]?)([^'""\)]*?\.(woff2?|ttf|otf|eot))\1\)";
		MatchEvaluator evaluator = new(ReplaceFontPath);
		string modifiedCss = Regex.Replace(cssContent, pattern, evaluator, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(10));
		return modifiedCss;
	}

	static string ReplaceFontPath(Match match)
	{
		string urlValue = match.Groups[2].Value;
		string quote = match.Groups[1].Value;

		if (!string.IsNullOrEmpty(urlValue) && (urlValue.Contains('/') || urlValue.Contains('\\')))
		{
			string extension = match.Groups[3].Value;
			if (!string.IsNullOrEmpty(extension))
			{
				string filename = Path.GetFileName(urlValue);
				return $"url({quote}{filename}{quote})";
			}
		}
		return match.Value;
	}
}
