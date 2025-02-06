using System.Text.RegularExpressions;
using EpubReader.Models;

namespace EpubReader.Service;

public static partial class InjectIntoHtml
{
	static readonly string disableTouchCSS = @"
		* {
				-webkit-touch-callout: none;
				-webkit-user-select: none;
				-khtml-user-select: none;
				-moz-user-select: none;
				-ms-user-select: none;
				user-select: none;
			}";

	static readonly string marginCss = @"
		body {
			margin: 1em;
		}";
	static string GenerateCSSFromString(string html, Settings settings)
	{
		if (string.IsNullOrWhiteSpace(html))
		{
			throw new ArgumentException("HTML content cannot be null or empty.", nameof(html));
		}

		// Construct the style tag with additional Css
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
	public static string InjectAllCss(string html, Book book, Settings settings)
	{
		if(string.IsNullOrEmpty(html))
		{
			System.Diagnostics.Debug.WriteLine("InjectAllCss: html is null or empty");
			return string.Empty;
		}
		// Remove existing <style> tags
		html = RemoveStyleTags(html);

		var otherCss = book.Css[^1].Content ?? string.Empty;
		otherCss += book.Css[0].Content ?? string.Empty;
		otherCss += disableTouchCSS;
		otherCss += marginCss;
		string styleTag = GenerateCSSFromString(html, settings);

		otherCss = FilterCss(otherCss, settings);
		styleTag += otherCss;


		// Inject the combined Css into the HTML
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

	[GeneratedRegex("<style[^>]*>.*?</style>", RegexOptions.Singleline, matchTimeoutMilliseconds: 20000)]
	private static partial Regex StyleTagRegex();

	[GeneratedRegex("font-size:\\s*\\d+px\\s*;", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 20000)]
	private static partial Regex FontSizeRegex();

	[GeneratedRegex("font-family:\\s*[^;]+?\\s*;", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 20000)]
	private static partial Regex FontFamilyRegex();

	static string RemoveStyleTags(string html)
	{
		return StyleTagRegex().Replace(html, string.Empty);
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
        // Define a timeout for the regex operations
        TimeSpan regexTimeout = TimeSpan.FromSeconds(20);

		// Handle standard img tags
		string imgPattern = $@"<img[^>]*src=[""']([^""']*{sourcePattern}[^""']*)[""'][^>]*>";
		
		htmlContent = Regex.Replace(htmlContent, imgPattern, match =>
        {
            string originalTag = match.Value;
            return originalTag.Replace(match.Groups[1].Value, newImageSource);
        }, RegexOptions.None, regexTimeout);

		// Handle img tags with additional attributes
		string imgPattern2 = @"<img[^>]*src=[""']([^""']*)[""'][^>]*>";

		// Replace the src attribute value with the new image source
		htmlContent = Regex.Replace(htmlContent, imgPattern2, match =>
		{
			string originalTag = match.Value;
			return originalTag.Replace(match.Groups[1].Value, newImageSource);
		}, RegexOptions.None, regexTimeout);

		// Handle SVG image tags
		string svgPattern = $@"<image[^>]*xlink:href=[""']([^""']*{sourcePattern}[^""']*)[""'][^>]*>";
        
		htmlContent = Regex.Replace(htmlContent, svgPattern, match =>
        {
            string originalTag = match.Value;
            return originalTag.Replace(match.Groups[1].Value, newImageSource);
        }, RegexOptions.None, regexTimeout);

        return htmlContent;
    }
}