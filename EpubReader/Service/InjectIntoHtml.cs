using System.Text;
using System.Text.RegularExpressions;
using System.Web;
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
		html = InjectCss(html, book, settings);
		html = ImageExtensions.ReplaceImageUrls(html, book.Images);
		html = InjectJavascript(html, JavaScriptConstants.AdjustTextSizeAndStyle + JavaScriptConstants.AdjustFontSize + JavaScriptConstants.AdjustSVGImages);
		html = RemoveGuttenBurgStyles(html);
		html = HttpUtility.HtmlEncode(html);
		html = HtmlBase(html);
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
		var css = new StringBuilder(StyleSheetConstants.RadiumCssConfig);
		if (!HasParagraphsRegex(html))
		{
			css.Append(StyleSheetConstants.ImageStyle);
		}
		css.Append(StyleSheetConstants.RadiumCssBefore);
		css.Append(StyleSheetConstants.RadiumCssAfter);
		var images = ExtractCssFiles(html);
		foreach (var item in images)
		{
			var file = book.Css.FirstOrDefault(x => x.FileName == Path.GetFileName(item)) ?? throw new InvalidOperationException("Css file not found");
			var filteredCSS = FilterCalibreCss(file.Content);
			filteredCSS = RemoveCssProperties(filteredCSS);
			//filteredCSS = ImageExtensions.ReplaceFontsWithBase64(filteredCSS, book.Fonts);
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

	static string HtmlBase(string html)
	{
		return $@"
		<!doctype html>
		<html lang=""en"">
		<head>
		  <title>Test</title>
		  <meta charset=""UTF-8"" />
		  <meta name=""viewport"" content=""width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no"">
		  <style type=""text/css"">
			html, body {{
			  margin: 0; 
			  padding: 0; 
			  box-sizing: border-box;
			}}
    
			body {{
			  display: flex;
			  height: 100vh;
			  width: 100%;
			  align-items: center;
			  justify-content: center;
			}}
    
			iframe {{
			  width: 90vw;
			  max-width: 100%;
			  height: 90vh;
			  max-height: 100%;
			  border: 0;
			  -webkit-user-select: none;
			  -moz-user-select: none;
			  -ms-user-select: none;
			  user-select: none;
			  /* May help later with Reading modes so that bg can be applied to whole page
				 You need to allowtransparency on the iframe though (and sanitize authors’ CSS) */
			  background-color: transparent;
			}}

			.rcss-input {{
			  position: absolute;
			  top: 0;
			  background-color: rgba(255, 255, 255, 0.5);
			  padding: 0.5rem;
			}}
		  </style>
		</head>
		<body>
<iframe title=""Book"" id=""page""  srcdoc=""{html}""></iframe>
		<script type=""text/javascript"">
			document.addEventListener(""DOMContentLoaded"", function() {{
			  const frame = document.getElementById(""page"");
  
			  const scrollLeft = () => {{
				const gap = parseInt(window.getComputedStyle(frame.contentWindow.document.documentElement).getPropertyValue(""column-gap""));
				frame.contentWindow.scrollTo(frame.contentWindow.scrollX - frame.contentWindow.innerWidth - gap, 0);
			  }};

			  const scrollRight = () => {{
				const gap = parseInt(window.getComputedStyle(frame.contentWindow.document.documentElement).getPropertyValue(""column-gap""));
				frame.contentWindow.scrollTo(frame.contentWindow.scrollX + frame.contentWindow.innerWidth + gap, 0);
			  }};

			  document.body.addEventListener(""click"", function(e) {{
				e.preventDefault();
				if (e.clientX > (window.innerWidth / 2)) {{
				  if(isHorizontallyScrolledToEnd()) {{
					window.location.href = 'https://runcsharp.next?true';
					return;
				}}
				  scrollRight();
				}} else {{
					if(isHorizontalScrollAtStart()) {{
						window.location.href = 'https://runcsharp.prev?true';
						return;
					}}
				  scrollLeft();
				}}
			  }});

			  document.body.addEventListener(""keydown"", function(e) {{
				if (e.keyCode == ""39"") {{
				  if(isHorizontallyScrolledToEnd()) {{
					window.location.href = 'https://runcsharp.next?true';
					return;
				}}
				  scrollRight();
				}} else if (e.keyCode == ""37"") {{
					if(isHorizontalScrollAtStart()) {{
						window.location.href = 'https://runcsharp.prev?true';
						return;	
					}}
				  scrollLeft();
				}}
			  }});
			}});
			
			function isHorizontallyScrolledToEnd() {{
				var frame = document.getElementById(""page"");
				if (!frame.contentWindow) {{
					window.location.href = 'https://runcsharp.next?false';
					return false;
				}}
				console.log(""isHorizontallyScrolledToEnd"");
				const contentDoc = frame.contentWindow.document.documentElement;
				const maxScrollLeft = contentDoc.scrollWidth - contentDoc.clientWidth;
				return Math.abs(frame.contentWindow.scrollX - maxScrollLeft) <= 10;
			}}
			
			function isHorizontalScrollAtStart() {{
				var frame = document.getElementById(""page"");
				if (!frame.contentWindow) {{
					console.log(""frame.contentWindow is null"");
					return false;
				}}
					console.log(frame.contentWindow.scrollX);
				return frame.contentWindow.scrollX <= 0;
			}}
			function scrollToHorizontalEnd() {{
			  const frame = document.getElementById(""page"");
			  window.location.href = 'https://runcsharp.test?true';
			  if (frame && frame.contentWindow && frame.contentWindow.document.readyState === 'complete') {{
				const contentDoc = frame.contentWindow.document.documentElement;
				const maxScrollLeft = contentDoc.scrollWidth - contentDoc.clientWidth;
				frame.contentWindow.scrollTo(maxScrollLeft, 0);
				window.location.href = 'https://runcsharp.test1?true';
			  }} else if (frame) {{
				// Iframe might not be loaded yet, wait for the 'load' event
				frame.onload = function() {{
					window.location.href = 'https://runcsharp.OnLoad?false';
				  scrollToHorizontalEnd(frame); // Call the function again when loaded
				  frame.onload = null; // Remove the event listener after it's executed once
				}};
			  }} else {{
				console.error(""Iframe element not provided."");
			  }}
			}}
		</script>
	</body>
	</html>";
	}
}