using System.Text.RegularExpressions;

namespace EpubReader.Util;

/// <summary>
/// Provides extension methods for updating file paths in HTML and CSS content.
/// </summary>
/// <remarks>This static class includes methods to modify HTML and CSS strings by replacing full file paths with
/// filenames. It is useful for scenarios where only the filenames are needed, such as when preparing content for
/// deployment to a different environment where paths may differ.</remarks>
public static class FilePathExtensions
{
	/// <summary>
	/// Updates the <c>src</c> attributes of <c>&lt;img&gt;</c> tags in the provided HTML string to use only the filenames.
	/// </summary>
	/// <remarks>This method uses a regular expression to identify <c>&lt;img&gt;</c> tags and extract the
	/// <c>src</c> attribute values. It then replaces the path in the <c>src</c> attribute with just the filename. The
	/// operation is case-insensitive and has a match timeout of 10 seconds.</remarks>
	/// <param name="htmlString">The HTML string containing <c>&lt;img&gt;</c> tags with <c>src</c> attributes to be updated.</param>
	/// <returns>A new HTML string with the <c>src</c> attributes of <c>&lt;img&gt;</c> tags replaced by their respective filenames.</returns>
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

	/// <summary>
	/// Updates the SVG and PNG links in the provided HTML content to use only the file names.
	/// </summary>
	/// <remarks>This method processes <c>&lt;img&gt;</c> tags with <c>src</c> attributes, <c>&lt;object&gt;</c>
	/// tags with <c>data</c> attributes, and <c>&lt;image&gt;</c> tags with <c>xlink:href</c> attributes, replacing the
	/// full paths of SVG and PNG files with just their file names.</remarks>
	/// <param name="html">The HTML content containing SVG and PNG links to be updated.</param>
	/// <returns>The modified HTML content with updated SVG and PNG links.</returns>
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

	/// <summary>
	/// Replaces font file paths in the provided CSS content with new paths.
	/// </summary>
	/// <param name="cssContent">The CSS content containing font file paths to be replaced.</param>
	/// <returns>The modified CSS content with updated font file paths.</returns>
	public static string SetFontFilenames(string cssContent)
	{
		string pattern = @"url\((['""]?)([^'""\)]*?\.(woff2?|ttf|otf|eot))\1\)";
		MatchEvaluator evaluator = new(ReplaceFontPath);
		string modifiedCss = Regex.Replace(cssContent, pattern, evaluator, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(10));
		return modifiedCss;
	}

	/// <summary>
	/// Replaces the font path in a CSS URL match with just the filename, preserving quotes.
	/// </summary>
	/// <remarks>This method is useful for simplifying font URLs in CSS by removing directory paths and leaving only
	/// the filename.</remarks>
	/// <param name="match">A <see cref="Match"/> object containing the CSS URL to be processed. The match should have groups representing the
	/// quote, URL, and file extension.</param>
	/// <returns>A string with the URL replaced by just the filename if the URL contains a path; otherwise, returns the original
	/// match value.</returns>
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