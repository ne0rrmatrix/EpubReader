using System.Text.RegularExpressions;
using EpubReader.Models;

namespace EpubReader.Service;

public static partial class InjectIntoHtml
{
	public static string InjectAllCss(string html, Book book, Settings settings)
	{
		if (string.IsNullOrEmpty(html))
		{
			return string.Empty;
		}
		html = RemoveStyleTags(html);

		var otherCss = book.Css[^1].Content ?? string.Empty;
		otherCss += book.Css[0].Content ?? string.Empty;
		otherCss += disableTouchCSS;
		string styleTag = GenerateCSSFromString(settings);

		otherCss = FilterCss(otherCss, settings);
		styleTag += otherCss;


		if (!string.IsNullOrEmpty(styleTag))
		{
			html = InjectCss(html, styleTag);
		}
		foreach (var image in book.Images)
		{
			html = ReplaceImageUrls(html, image.FileName, image.ImageUrl);
		}
		return html;
	}

	static string RemoveStyleTags(string html)
	{
		return StyleTagRegex().Replace(html, string.Empty);
	}

	static string GenerateCSSFromString(Settings settings) // Modified to accept settings directly
	{
		string styleTag = string.Empty;

		if (!string.IsNullOrEmpty(settings.BackgroundColor) || !string.IsNullOrEmpty(settings.TextColor) || settings.FontSize > 0 || !string.IsNullOrEmpty(settings.FontFamily))
		{
			styleTag = $@"
                body {{
                    {(string.IsNullOrEmpty(settings.BackgroundColor) ? "" : $"background-color: {settings.BackgroundColor};")}
                    {(string.IsNullOrEmpty(settings.TextColor) ? "" : $"color: {settings.TextColor};")}
                    {(settings.FontSize > 0 ? $"font-size: {settings.FontSize}px !important;" : "")}
                    {(string.IsNullOrEmpty(settings.FontFamily) ? "" : $"font-family: {settings.FontFamily};")}
                }}";
		}
		return styleTag;
	}

	static string FilterCss(string css, Settings settings)
	{
		if (string.IsNullOrEmpty(css))
		{
			return string.Empty;
		}
		// Remove font-size and font-family from the Css
		if (settings.FontSize > 0)
		{
			css = FontSizeRegex().Replace(css, string.Empty);
		}
		if (!string.IsNullOrEmpty(settings.FontFamily))
		{
			css = FontFamilyRegex().Replace(css, string.Empty);
		}
		return css;
	}

	static string InjectCss(string html, string css)
	{
		// Assuming you want to inject the Css into the <head> section
		int headEndTagIndex = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
		if (headEndTagIndex >= 0)
		{
			html = html.Insert(headEndTagIndex, $"<style>{css}</style>");
		}
		else
		{
			// If no <head> tag is found, prepend the style to the HTML
			html = $"<style>{css}</style>" + html;
		}
		return html;
	}
	static string ReplaceImageUrls(string htmlContent, string sourcePattern, string newImageSource)
	{
		TimeSpan regexTimeout = TimeSpan.FromSeconds(20);

		string imgPattern = $@"<img[^>]*src=[""']([^""']*{sourcePattern}[^""']*)[""'][^>]*>";
		htmlContent = Regex.Replace(htmlContent, imgPattern, match =>
		{
			string originalTag = match.Value;
			return originalTag.Replace(match.Groups[1].Value, newImageSource);
		}, RegexOptions.None, regexTimeout);

		string imgPattern2 = @"<img[^>]*src=[""']([^""']*)[""'][^>]*>"; // For other img tags

		htmlContent = Regex.Replace(htmlContent, imgPattern2, match =>
		{
			string originalTag = match.Value;
			return originalTag.Replace(match.Groups[1].Value, newImageSource);
		}, RegexOptions.None, regexTimeout);


		string svgPattern = $@"<image[^>]*xlink:href=[""']([^""']*{sourcePattern}[^""']*)[""'][^>]*>";

		return Regex.Replace(htmlContent, svgPattern, match =>
		{
			string originalTag = match.Value;
			return originalTag.Replace(match.Groups[1].Value, newImageSource);
		}, RegexOptions.None, regexTimeout);
	}


	[GeneratedRegex("<style[^>]*>.*?</style>", RegexOptions.Singleline, matchTimeoutMilliseconds: 20000)]
	private static partial Regex StyleTagRegex();

	[GeneratedRegex("font-size:\\s*\\d+px\\s*;", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 20000)]
	private static partial Regex FontSizeRegex();

	[GeneratedRegex("font-family:\\s*[^;]+?\\s*;", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 20000)]
	private static partial Regex FontFamilyRegex();

	static readonly string disableTouchCSS = @"
		* {
				-webkit-touch-callout: none;
				-webkit-user-select: none;
				-khtml-user-select: none;
				-moz-user-select: none;
				-ms-user-select: none;
				user-select: none;
			}
			body {
			margin: 1em;
		}";
}