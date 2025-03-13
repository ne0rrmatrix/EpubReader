using System.Text;
using System.Text.RegularExpressions;
using EpubReader.Models;
using EpubReader.Util;
using HtmlAgilityPack;
using ImageExtensions = EpubReader.Util.ImageExtensions;

namespace EpubReader.Service;

public static partial class InjectIntoHtml
{
	[GeneratedRegex(@"@import\s+url\(['""](.+?)['""]\)", RegexOptions.Compiled, matchTimeoutMilliseconds: 20000)]
	private static partial Regex StyleSheet();

	[GeneratedRegex(@"<p(\s[^>]*)?>", RegexOptions.Compiled, matchTimeoutMilliseconds: 20000)]
	private static partial Regex HasParagraphs();

	[GeneratedRegex(@"<style[^>]*?>[\s\S]*?</style>|<style[^>]*?/>", RegexOptions.Compiled, matchTimeoutMilliseconds: 20000)]
	private static partial Regex WithoutStyles();

	[GeneratedRegex(@"<script[^>]*?>[\s\S]*?</script>|<script[^>]*?/>", RegexOptions.Compiled, matchTimeoutMilliseconds: 20000)]
	private static partial Regex WithoutScripts();

	[GeneratedRegex("<style[^>]*>.*?</style>", RegexOptions.Singleline, matchTimeoutMilliseconds: 20000)]
	private static partial Regex StyleTagRegex();

	static readonly TimeSpan regexTimeout = TimeSpan.FromSeconds(20);

	static readonly string[] projectGuttenBurgStyles =
		[
			@"\.xhtml_center\s*\{[^}]*\}\s*",
			@"\.xhtml_center\s+table\s*\{[^}]*\}\s*",
			@"body\s*\{\s*text-align:\s*justify[^}]*\}\s*",
			@"@media\s+screen\s*\{[^}]*body\s*\{[^}]*\}[^}]*\}\s*",
			@"\.pagedjs_page_content\s*>\s*div\s*\{[^}]*\}\s*"
		];

	public static string UpdateHtml(string html, Book book, Settings settings)
	{
		if (string.IsNullOrEmpty(html))
		{
			return string.Empty;
		}
		html = ImageExtensions.FixImageTags(html);
		html = RemoveScriptAndStyleTags(html);
		html = StyleTagRegex().Replace(html, string.Empty);
		html = InjectCss(html, book, settings);
		html = ImageExtensions.ReplaceImageUrls(html, book.Images);
		html = InjectJavascript(html, JavaScriptConstants.DisableScroll + JavaScriptConstants.ButtonNavigation + JavaScriptConstants.AdjustTextSizeAndStyle + JavaScriptConstants.AdjustFontSize + JavaScriptConstants.AdjustSVGImages);
		html = AddDivContainer(html);
		html = RemoveGuttenBurgStyles(html);
		return html;
	}

	static string RemoveGuttenBurgStyles(string htmlContent)
	{
		// Create HTML document
		var doc = new HtmlDocument();
		doc.LoadHtml(htmlContent);

		// Find all style tags
		var styleTags = doc.DocumentNode.SelectNodes("//style");

		if (styleTags != null)
		{
			foreach (var styleTag in styleTags)
			{
				string cssContent = styleTag.InnerHtml;

				// Remove the specific CSS styles using regex
				cssContent = RemoveSpecificCssRules(cssContent);

				// Update the style tag content
				styleTag.InnerHtml = cssContent;

				// If style tag is now empty, remove it
				if (string.IsNullOrWhiteSpace(styleTag.InnerHtml))
				{
					styleTag.Remove();
				}
			}
		}

		return doc.DocumentNode.OuterHtml;
	}

	static string RemoveSpecificCssRules(string cssContent)
	{
		// Remove each pattern from the CSS content
		foreach (var pattern in projectGuttenBurgStyles)
		{
			cssContent = Regex.Replace(cssContent, pattern, string.Empty, RegexOptions.Compiled, regexTimeout);
		}

		return cssContent;
	}

	static bool HasParagraphsRegex(string htmlString)
	{
		// This pattern looks for opening <p> tags with optional attributes
		return HasParagraphs().IsMatch(htmlString);
	}

	static string InjectCss(string html, Book book, Settings settings)
	{
		int numberOfColumns = 1;
		if ((OperatingSystem.IsWindows() || OperatingSystem.IsMacCatalyst()) && HasParagraphsRegex(html))
		{
			numberOfColumns = 2;
		}

		var css = new StringBuilder(StyleSheetConstants.GetStyle(numberOfColumns));
		if (!HasParagraphsRegex(html))
		{
			css.Append(StyleSheetConstants.ImageStyle);
		}
		var images = ExtractCssFiles(html);
		foreach (var item in images)
		{
			var file = book.Css.FirstOrDefault(x => x.FileName == Path.GetFileName(item)) ?? throw new InvalidOperationException("Css file not found");
			var filteredCSS = FilterCalibreCss(file.Content);
			filteredCSS = RemoveCssProperties(filteredCSS);
			css.Append(ImageExtensions.ReplaceCssUrls(filteredCSS, book.Images));
		}
		
		var styleTag = new StringBuilder();
		styleTag.Append(GenerateCSSFromString(settings));
		styleTag.Append(css);

		int headEndTagIndex = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
		if (headEndTagIndex >= 0)
		{
			html = html.Insert(headEndTagIndex, $"<style>{styleTag}</style>");
		}

		return html;
	}

	static List<string> ExtractCssFiles(string htmlString)
	{
		List<string> cssFiles = [];

		// Load HTML document
		HtmlDocument doc = new();
		doc.LoadHtml(htmlString);

		// Find all link tags with rel="stylesheet"
		var linkNodes = doc.DocumentNode.SelectNodes("//link[@rel='stylesheet']");
		if (linkNodes != null)
		{
			foreach (var link in linkNodes)
			{
				string href = link.GetAttributeValue("href", "");
				if (!string.IsNullOrEmpty(href))
				{
					cssFiles.Add(href);
				}
			}
		}

		// Find @import statements in style tags
		var styleNodes = doc.DocumentNode.SelectNodes("//style");
		if (styleNodes is not null)
		{
			cssFiles.AddRange(from style in styleNodes
							  let styleContent = style.InnerHtml// Use regex to find @import url statements
							  let matches = StyleSheet().Matches(styleContent)
							  from Match match in matches
							  where match.Groups.Count > 1
							  select match.Groups[1].Value);
		}

		return cssFiles;
	}

	static string FilterCalibreCss(string? cssString)
	{
		if (string.IsNullOrEmpty(cssString))
		{
			return string.Empty;
		}
		string regexPattern = @"\.calibre(1)?\s*\{[^}]*\}";
		Regex regex = new(regexPattern, RegexOptions.Compiled, regexTimeout);

		return regex.Replace(cssString, string.Empty); // Replace matches with empty string
	}

	static string RemoveCssProperties(string htmlString)
	{
		// Generated Regex for matching CSS rules
		var cssRuleRegex = new Regex(@"((?:p|body|html)(?:\s*,\s*(?:p|body|html))*)(\s*\{[^}]*\})", RegexOptions.IgnoreCase | RegexOptions.Singleline, regexTimeout);

		// Generated Regex for removing margin, padding, and text-indent
		var propertyRemovalRegex = new Regex(@"(^|\s|\;)\s*(margin|padding|text-indent)\s*:[^;]*;?", RegexOptions.IgnoreCase, regexTimeout);

		// Generated Regex for cleaning up semicolons and whitespace
		var semicolonCleanupRegex = new Regex(@"\s*;\s*}", RegexOptions.Compiled, regexTimeout);
		var emptyBlockCleanupRegex = new Regex(@"{\s*}", RegexOptions.Compiled, regexTimeout);
		var multipleSpaceCleanupRegex = new Regex(@"\s{2,}", RegexOptions.Compiled, regexTimeout);

		return cssRuleRegex.Replace(htmlString, match =>
		{
			string selectors = match.Groups[1].Value;
			string cssBlock = match.Groups[2].Value;

			cssBlock = propertyRemovalRegex.Replace(cssBlock, "$1");
			cssBlock = semicolonCleanupRegex.Replace(cssBlock, " }");
			cssBlock = emptyBlockCleanupRegex.Replace(cssBlock, "{ }");
			cssBlock = multipleSpaceCleanupRegex.Replace(cssBlock, " ");

			return selectors + cssBlock;
		});
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
               	{(string.IsNullOrEmpty(settings.FontFamily) ? "" : $"font-family: {settings.FontFamily};")}
            }}
			p {{ 
				{(settings.FontSize > 0 ? $"font-size: {settings.FontSize}px !important;" : "")}
			}}";
	}
	
	static string InjectJavascript(string html, string javascript)
	{
		int headEndTagIndex = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
		if (headEndTagIndex >= 0)
		{
			html = html.Insert(headEndTagIndex, $@"<script>{javascript}</script>");
		}

		return html;
	}

	static string RemoveScriptAndStyleTags(string htmlString)
	{
		if (string.IsNullOrEmpty(htmlString))
		{
			return htmlString;
		}

		// Remove script tags with all their attributes and content
		string withoutScripts = WithoutScripts().Replace(htmlString, string.Empty);

		// Remove style tags with all their attributes and content
		string withoutStyles = WithoutStyles().Replace(withoutScripts, string.Empty);

		// Return the cleaned string
		return withoutStyles.Trim();
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
}