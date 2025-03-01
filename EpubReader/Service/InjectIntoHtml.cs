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

		var cssContent = BuildCssContent(html, book, settings);
		html = RemoveExistingStyleTags(html);
		html = InjectCss(html, cssContent);
		html = ReplaceImageUrls(html, book.Images);
		html = InjectJavascript(html, disableScroll + jsButtons);
		html = AddDivContainer(html);

		return html;
	}

	static string BuildCssContent(string html, Book book, Settings settings)
	{
		var css = new StringBuilder(style);
		var cssList = ExtractCssFileNames(html);

		foreach (var cssFile in cssList.Select(item => book.Css.Find(x => item.Contains(x.FileName))))
		{
			css.Append(cssFile?.Content);
		}

		var styleTag = new StringBuilder();
		styleTag.Append(GenerateCSSFromString(settings));
		styleTag.Append(FilterCss(css.ToString(), settings));
		styleTag.Append(css);

		return styleTag.ToString();
	}

	static List<string> ExtractCssFileNames(string html)
	{
		const string pattern = @"<link\s+href=""([^""]+\.css)""\s+rel=""stylesheet""";
		var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase, regexTimeout);

		return [.. matches.Select(match => Path.GetFileName(match.Groups[1].Value))];
	}

	static string GenerateCSSFromString(Settings settings)
	{
		if (string.IsNullOrEmpty(settings.BackgroundColor) && string.IsNullOrEmpty(settings.TextColor) && settings.FontSize <= 0 && string.IsNullOrEmpty(settings.FontFamily))
		{
			return string.Empty;
		}

		return $@"
            body {{
                {(string.IsNullOrEmpty(settings.BackgroundColor) ? "" : $"background-color: {settings.BackgroundColor};")}
                {(string.IsNullOrEmpty(settings.TextColor) ? "" : $"color: {settings.TextColor};")}
                {(settings.FontSize > 0 ? $"font-size: {settings.FontSize}px !important;" : "")}
                {(string.IsNullOrEmpty(settings.FontFamily) ? "" : $"font-family: {settings.FontFamily};")}
            }}";
	}

	static string FilterCss(string css, Settings settings)
	{
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
		int headEndTagIndex = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
		if (headEndTagIndex >= 0)
		{
			html = html.Insert(headEndTagIndex, $"<style>{css}</style>");
		}
		else
		{
			html = $"<style>{css}</style>" + html;
		}
		return html;
	}

	static string ReplaceImageUrls(string html, List<Models.Image> images)
	{
		foreach (var image in images)
		{
			html = ReplaceImageUrl(html, image.FileName, image.ImageUrl);
		}
		return html;
	}

	static string ReplaceImageUrl(string htmlContent, string sourcePattern, string newImageSource)
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
		
		htmlContent = Regex.Replace(htmlContent, svgPattern, match =>
		{
			string originalTag = match.Value;
			return originalTag.Replace(match.Groups[1].Value, newImageSource);
		}, RegexOptions.None, regexTimeout);
		return htmlContent;
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

		const string openingBodyRegex = @"<body\s*([^>]*)>";
		const string closingBodyRegex = @"</body>";

		var openingBodyMatch = Regex.Match(html, openingBodyRegex, RegexOptions.IgnoreCase, regexTimeout);
		var closingBodyMatch = Regex.Match(html, closingBodyRegex, RegexOptions.IgnoreCase, regexTimeout);

		if (!openingBodyMatch.Success || !closingBodyMatch.Success)
		{
			return html;
		}

		var result = new StringBuilder();
		result.Append(html.AsSpan(0, openingBodyMatch.Index + openingBodyMatch.Length));
		result.Append("<div id=\"scrollContainer\">");
		result.Append(html.AsSpan(openingBodyMatch.Index + openingBodyMatch.Length, closingBodyMatch.Index - (openingBodyMatch.Index + openingBodyMatch.Length)));
		result.Append("</div></body>");
		result.Append(html.AsSpan(closingBodyMatch.Index + closingBodyMatch.Length));

		return result.ToString();
	}

	static string RemoveExistingStyleTags(string html)
	{
		return StyleTagRegex().Replace(html, string.Empty);
	}

	[GeneratedRegex("<style[^>]*>.*?</style>", RegexOptions.Singleline, matchTimeoutMilliseconds: 20000)]
	private static partial Regex StyleTagRegex();

	[GeneratedRegex("font-size:\\s*\\d+px\\s*;", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 20000)]
	private static partial Regex FontSizeRegex();

	[GeneratedRegex("font-family:\\s*[^;]+?\\s*;", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 20000)]
	private static partial Regex FontFamilyRegex();

	static readonly string disableScroll = @"
        window.addEventListener('wheel', function(event) {
            event.preventDefault();
        }, { passive: false });

        window.addEventListener('touchmove', function(event) {
            event.preventDefault();
        }, { passive: false });";

	static readonly string style = @"
        ::-webkit-scrollbar {
            display: none;
        }

        * {
            -webkit-touch-callout: none;
        }

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
                return false;
            }
            return element.scrollLeft === 0;
        }

        function isHorizontallyScrolledToEnd() {
            var element = document.getElementById(""scrollContainer"");
            if (!element) {
                return false;
            }
            const maxScrollLeft = element.scrollWidth - element.clientWidth;
            return Math.abs(element.scrollLeft - maxScrollLeft) <= 1;
        }";
}