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

	public static string UpdateHtml(string html, Book book)
	{
		if (string.IsNullOrEmpty(html))
		{
			return string.Empty;
		}
		html = InjectCss(html, book);
		html = ImageExtensions.ReplaceImageUrls(html, book.Images);
		html = InjectJavascript(html, JavaScriptConstants.AdjustSVGImages);
		html = HttpUtility.HtmlEncode(html);
		html = HtmlBase(html);
		return html;
	}

	static string InjectCss(string html, Book book)
	{
		var css = new StringBuilder(StyleSheetConstants.RadiumCssConfig);
		css.Append(StyleSheetConstants.RadiumCssBefore);
		css.Append(StyleSheetConstants.RadiumCssAfter);
		var images = ExtractCssFiles(html);
		bool hasOpenSourceFonts = false;
		foreach (var item in images)
		{
			var file = book.Css.FirstOrDefault(x => x.FileName == Path.GetFileName(item)) ?? throw new InvalidOperationException("Css file not found");
			hasOpenSourceFonts = FontDeclarationValidator.ContainsOpenSourceFontAlternatives(file.Content);
			var filteredCSS = ImageExtensions.ReplaceFontsWithBase64(file.Content, book.Fonts);
			css.Append(ImageExtensions.ReplaceCssUrls(filteredCSS, book.Images));
		}

		System.Diagnostics.Trace.TraceInformation($"CSS contains proper open source font alternatives: {hasOpenSourceFonts}");

		var styleTag = new StringBuilder();
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
			  width: 80vw;
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

			}}
		  </style>
		</head>
		<body>
<iframe title=""Book"" id=""page""  srcdoc=""{html}""  allowtransparency=""true"" scrolling=""no""></iframe>
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
			  if (frame && frame.contentWindow && frame.contentWindow.document.readyState === 'complete') {{
				const contentDoc = frame.contentWindow.document.documentElement;
				const maxScrollLeft = contentDoc.scrollWidth - contentDoc.clientWidth;
				frame.contentWindow.scrollTo(maxScrollLeft, 0);
			  }} else if (frame) {{
				// Iframe might not be loaded yet, wait for the 'load' event
				frame.onload = function() {{
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