using System.Text;
using System.Text.RegularExpressions;
using EpubReader.Models;

namespace EpubReader.Service;

public static partial class InjectIntoHtml
{
	static readonly TimeSpan regexTimeout = TimeSpan.FromSeconds(20);
	public static string InjectAllCss(string html, Book book, Settings settings)
	{
		if (string.IsNullOrEmpty(html))
		{
			return string.Empty;
		}
		html = RemoveStyleTags(html);

		var otherCss = book.Css[^1].Content ?? string.Empty;
		otherCss += disableTouchCSS;
		otherCss += style;
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
		var js = disableScrollBars + disableScroll + jsButtons;
		html = InjectJavascript(html, js);
		html = AddDivContainer(html);
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

	static string InjectJavascript(string html, string javascript)
	{
		int headEndTagIndex = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
		if (headEndTagIndex >= 0)
		{
			html = html.Insert(headEndTagIndex, $@"<script>{javascript}</script>");
		}
		else
		{
			// If no <head> tag is found, prepend the style to the HTML
			html = $"<script>{javascript}</script>" + html;
		}
		return html;
	}

	static string AddDivContainer(string html)
	{
		if (string.IsNullOrEmpty(html))
		{
			return html;
		}

		// Regex to find opening and closing body tags, allowing for attributes and whitespace.
		string openingBodyRegex = @"<body\s*([^>]*)>";
		string closingBodyRegex = @"</body>";

		// Check for body tags.
		Match openingBodyMatch = Regex.Match(html, openingBodyRegex, RegexOptions.IgnoreCase, regexTimeout);
		Match closingBodyMatch = Regex.Match(html, closingBodyRegex, RegexOptions.IgnoreCase, regexTimeout);

		if (!openingBodyMatch.Success || !closingBodyMatch.Success)
		{
			return html; // Return original if no body tags found.
		}

		StringBuilder result = new();

		// Append the part before the opening body.
		result.Append(html.AsSpan(0, openingBodyMatch.Index + openingBodyMatch.Length));

		// Append the opening div.
		result.Append("<div id=\"scrollContainer\">");

		// Append the content between the body tags.
		result.Append(html.AsSpan(openingBodyMatch.Index + openingBodyMatch.Length, closingBodyMatch.Index - (openingBodyMatch.Index + openingBodyMatch.Length)));

		// Append the closing div and the closing body tag.
		result.Append("</div></body>");

		// Append the part after the closing body.
		result.Append(html.AsSpan(closingBodyMatch.Index + closingBodyMatch.Length));

		return result.ToString();
	}


	[GeneratedRegex("<style[^>]*>.*?</style>", RegexOptions.Singleline, matchTimeoutMilliseconds: 20000)]
	private static partial Regex StyleTagRegex();

	[GeneratedRegex("font-size:\\s*\\d+px\\s*;", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 20000)]
	private static partial Regex FontSizeRegex();

	[GeneratedRegex("font-family:\\s*[^;]+?\\s*;", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 20000)]
	private static partial Regex FontFamilyRegex();

	static readonly string disableTouchCSS = @"
		*	{
				-webkit-touch-callout: none;
				-webkit-user-select: none;
				-khtml-user-select: none;
				-moz-user-select: none;
				-ms-user-select: none;
				user-select: none;
			}";

	static readonly string disableScrollBars = @"
		function disableScrollBars() {
		document.querySelector('body').style.overflow = 'scroll';
		var style = document.createElement('style');
		style.type = 'text/css';
		style.innerHTML = '::-webkit-scrollbar { display: none }';
		document.getElementsByTagName('body')[0].appendChild(style);}";

	static readonly string disableScroll = @"
		window.addEventListener('wheel', function(event) {
			event.preventDefault();
		}, { passive: false });

		window.addEventListener('touchmove', function(event) {
			event.preventDefault();
		}, { passive: false });";

	static readonly string style = @"
		#scrollContainer {
			columns: 1;
			overflow-x: auto;
			height: 100vh;
		}

		#scrollContainer p {
			text-align: justify;
			margin-left: 2em;
			margin-right: 2em;
		}";

	static readonly string jsButtons = @"
		function nextPage() {
        document.getElementById(""scrollContainer"").scrollLeft += window.visualViewport.width;
		}

		function prevPage() {
			document.getElementById(""scrollContainer"").scrollLeft -= window.visualViewport.width;
		}
	
		function isHorizontalScrollAtStart() {
			var element = document.getElementById(""scrollContainer"");
			if (!element) {
				// Handle cases where the element is not provided or doesn't exist.
				return false; // Or false, depending on how you want to handle this case.
			}
			return element.scrollLeft === 0;
		}

		 function isHorizontallyScrolledToEnd() {
			var element = document.getElementById(""scrollContainer"");
			if (!element) {
				return false; // Handle cases where the element doesn't exist.
			}
		  const maxScrollLeft = element.scrollWidth - element.clientWidth;
		  return Math.abs(element.scrollLeft - maxScrollLeft) <= 1; // Using a small tolerance to account for potential rounding errors.
		}";
}